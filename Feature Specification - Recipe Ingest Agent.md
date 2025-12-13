# Feature Specification: Recipe Ingest Agent

**Feature Name:** Recipe Ingest Agent  
**Target Platform:** CookBook Agent Platform (.NET 10, Aspire-orchestrated microservices)  
**Status:** Draft (proposed)  
**Primary Goal:** Enable users (and agents) to find recipes on the public internet, convert them into the platform’s canonical `Recipe` document format, and ingest them into Cosmos DB with strong provenance + policy-driven paraphrasing.

---

## 1. Background and Problem Statement

The platform currently supports:
- Natural-language recipe search against stored recipes
- AI-scored candidates and nutrition analysis
- Artifact generation (recipe cards, shopping lists)
- Real-time task progress via Redis Streams + SignalR

However, there is no structured workflow for discovering and importing recipes from the internet into the canonical `Recipe` domain model stored in Cosmos DB.

This feature introduces an agentic, task-based import pipeline (“Recipe Ingest Agent”) that:
1) Finds candidate recipe URLs on the internet,
2) Fetches and extracts recipe data (prefer structured data when available),
3) Produces a validated canonical `RecipeDraft`,
4) Enforces an explicit “no large verbatim blocks” paraphrase policy,
5) Requires human approval before persisting into Cosmos,
6) Optionally runs a second-phase normalization pass.

---

## 2. Goals and Non-Goals

### 2.1 Goals
1. **Structured ingestion:** Produce a `RecipeDraft` that matches the canonical `Recipe` schema.
2. **Provenance-first:** Always capture source URL + retrieval metadata.
3. **Policy-driven paraphrasing:** Explicitly instruct the LLM to avoid large verbatim text blocks and to rephrase/summarize when the source contains large copied segments.
4. **Human-in-the-loop commit:** No recipe is persisted without explicit approval.
5. **Two-phase workflow:** Support a fast “faithful import” followed by optional “normalization”.
6. **Observability:** Emit phase progress + artifact references via existing TaskState/SignalR patterns.

### 2.2 Non-Goals
- Bulk crawling of sites or large-scale scraping.
- Bypassing paywalls, captchas, or other access controls.
- Guaranteed compliance with every site’s Terms of Service (ToS). The platform will implement best-effort controls and provide operator-configurable allow/deny lists.
- Perfect ingredient NLP from free-form text in v1 (normalization is optional and iterative).

---

## 3. Definitions

- **Canonical Recipe:** The platform’s persisted `Recipe` document (Cosmos container `recipes`).
- **RecipeDraft:** A wrapper object that contains the canonical `Recipe` payload plus provenance, validation output, similarity/guardrail reports, and artifact references. The canonical `Recipe` remains the persistence model.
- **Normalization:** A second-phase enrichment that improves structure/consistency (units, canonical names, step cleanup) without changing cooking meaning.
- **Prompt Template:** A versioned template used to generate LLM instructions for a phase (discovery, extraction, normalization).
- **Artifact:** Blob-stored content related to a task (snapshots, extracted JSON-LD, draft JSON, diff patch).

---

## 4. User Experience

### 4.1 Primary User Flows

#### Flow A — Import by query
1. User enters a natural-language query (optionally diet/cuisine constraints).
2. System runs Recipe Ingest Agent:
   - discovers candidate URLs,
   - fetches content,
   - extracts structured recipe data,
   - produces a `RecipeDraft`.
3. UI shows:
   - list of candidate sources
   - extracted draft recipe
   - validation warnings/errors
   - provenance details
4. User edits if needed, then clicks **Commit** to persist into Cosmos.

#### Flow B — Import by URL
1. User pastes a recipe URL.
2. System skips discovery and performs fetch/extract/validate.
3. UI review + commit.

#### Flow C — Normalize (optional)
1. From a draft or stored recipe, user selects **Normalize**.
2. System runs a normalization task producing a patch/diff.
3. UI shows diff and user accepts/rejects changes.

### 4.2 UI Requirements (Blazor Server)
- New “Web Import” entry point: *Recipe Ingest Agent*
- Views:
  - **Import Wizard** (Query/URL → progress → review)
  - **Prompt Selector** (choose prompt version; optional override text)
  - **Diff Viewer** (for normalization patches)
- Real-time progress via SignalR updates matching existing task event patterns.

### 4.3 Review Lifecycle and Expiration

When a task reaches `Ingest.ReviewReady`, it enters a waiting state until a human takes action:

- Actions: **Commit**, **Reject**, **Normalize** (optional), **Edit then Commit**
- Expiration: drafts expire after **7 days** (configurable). Expired drafts cannot be committed.
- Concurrency: only one commit is allowed per `taskId`. The commit endpoint MUST be idempotent and reject duplicate commits.

Task terminal states:
- `Committed`, `Rejected`, `Expired`, `Failed`

#### Expiration Enforcement

Expiration is enforced via **lazy check at commit time** (primary) with optional background cleanup:

1. **Commit-time check (required):** The commit endpoint validates `TaskState.LastUpdated + ExpirationDays < Now`. If expired, return `410 Gone` with error details.
2. **Background job (optional):** A scheduled job scans `ReviewReady` tasks older than the expiration window and transitions them to `Expired` status. This provides proactive cleanup and UI consistency.

#### Commit Concurrency Control

The commit endpoint uses **optimistic concurrency** via Cosmos ETag:

1. Read the task document and verify status is `ReviewReady` and not expired.
2. Attempt to update status to `Committed` with `if-match: {etag}`.
3. If ETag mismatch (concurrent commit), return `409 Conflict`.
4. If already `Committed`, return `200 OK` (idempotent success).

---

## 5. Architecture Overview

### 5.1 Placement in Existing System

Recipe Ingest Agent extends the existing pipeline pattern:
- Gateway receives a task request (`POST /api/tasks`)
- Orchestrator coordinates phases and agent execution
- Redis Streams queues work and TaskState provides observable status
- Blob Storage stores artifacts
- Cosmos persists final canonical recipe documents

### 5.2 Agent Types and Phases

Introduce a new `AgentType`:
- `Ingest` (new)

**Phase model (recommended):** One AgentType with multiple phases controlled via `TaskState.CurrentPhase`.

#### Ingest Phases
1. `Ingest.Discover` (query → candidate URLs) *(optional for URL imports)*  
   - Implemented via `ISearchProvider` (default: Search API provider).  
2. `Ingest.Fetch` (URL → fetch response + sanitized text snapshot)  
3. `Ingest.Extract` (snapshot → `RecipeDraft`)  
4. `Ingest.Validate` (`RecipeDraft` → validation report + repair attempts if needed)  
   - `Ingest.RepairJson` (optional; schema/JSON repair loop)  
   - `Ingest.RepairParaphrase` (optional; policy repair loop for verbatim blocks)  
5. `Ingest.ReviewReady` (publish result to UI; waits for human action)

#### Normalize Phases
- `Ingest.Normalize` (draft/stored recipe → patch/diff + validation)

### 5.3 Provider Selection

**Important:** `PhaseProviders` applies to **LLM-backed phases** only. `Ingest.Discover` is not an LLM phase by default; it is executed through an `ISearchProvider`.

Default provider suggestions (operator-configurable):

- `Ingest.Extract` → LLM provider optimized for structured JSON (e.g., OpenAI)  
- `Ingest.Normalize` → same as above  
- `Ingest.RepairJson` / `Ingest.RepairParaphrase` → same as above  

Operators may override provider routing per phase using the existing `LlmRouterOptions.PhaseProviders` mechanism.

## 5.4 Discovery Provider (Search)

`Ingest.Discover` uses a pluggable search provider abstraction:

```csharp
public interface ISearchProvider
{
    Task<IReadOnlyList<SearchCandidate>> SearchAsync(SearchRequest request, CancellationToken ct);
}

public record SearchRequest(string Query, Dictionary<string, string?> Constraints);
public record SearchCandidate(string Url, string Title, string Snippet, string? SiteName, double Score);
```

Implementations (examples):
- Search API-backed providers (Bing, Google Custom Search, etc.)
- Curated sources provider (operator-maintained allowlist of sites)
- Optional "LLM browsing" provider behind a feature flag (not default)

Search providers must support:
- API key configuration (when applicable)
- per-domain rate limits
- quota/cost controls
- allow/deny lists

### 5.5 Candidate Selection and Multi-URL Handling

When discovery returns multiple candidates, the system applies the following behavior:

#### Candidate Limits
- Discovery returns up to **10 candidates** (configurable via `IngestOptions.MaxDiscoveryCandidates`).
- Candidates are ranked by the search provider's `Score` (descending).

#### Single-Recipe Import (v1 default)
In v1, the system processes **only the top-ranked candidate** automatically:
1. Discovery returns ranked candidates.
2. Top candidate proceeds to `Ingest.Fetch`.
3. All candidates are stored in artifacts (`candidates.json`) for audit/UI display.
4. UI shows the candidate list; user can restart import with a different candidate if needed.

#### Multi-Recipe Import (future)
A future enhancement may allow parallel extraction of multiple candidates, presenting all drafts for user selection. This is **not in scope for v1**.

```csharp
public class IngestOptions
{
    public const string SectionName = "Ingest";

    /// <summary>
    /// Maximum number of candidates returned by discovery.
    /// </summary>
    public int MaxDiscoveryCandidates { get; set; } = 10;

    /// <summary>
    /// Default expiration for drafts in ReviewReady state (days).
    /// </summary>
    public int DraftExpirationDays { get; set; } = 7;

    /// <summary>
    /// Maximum content size to fetch (bytes).
    /// </summary>
    public int MaxFetchSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Character budget for sanitized content sent to LLM.
    /// </summary>
    public int ContentCharacterBudget { get; set; } = 60_000;

    /// <summary>
    /// Whether to respect robots.txt when fetching URLs (best-effort).
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// Maximum size for individual artifacts (bytes). Larger content is chunked/compressed.
    /// </summary>
    public int MaxArtifactSizeBytes { get; set; } = 1 * 1024 * 1024; // 1 MB

    /// <summary>
    /// Circuit breaker settings for failing domains.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration for domain-level failure handling.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before blocking a domain.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time window in which failures are counted (minutes).
    /// </summary>
    public int FailureWindowMinutes { get; set; } = 10;

    /// <summary>
    /// Duration to block a domain after threshold is reached (minutes).
    /// </summary>
    public int BlockDurationMinutes { get; set; } = 30;
}
```

---

## 6. Data Model Changes

### 6.1 Add `RecipeSource` provenance

Add an optional `Source` property to the canonical recipe:

```csharp
public record RecipeSource
{
    public required string Url { get; init; }
    
    /// <summary>
    /// Stable hash of the normalized URL for efficient duplicate detection queries.
    /// Computed as: Base64Url(SHA256(NormalizeUrl(Url)))[..22]
    /// Uses base64url encoding (RFC 4648) to avoid URL-unsafe characters.
    /// </summary>
    public string? UrlHash { get; init; }
    
    public string? SiteName { get; init; }
    public string? Author { get; init; }
    public DateTime RetrievedAt { get; init; }
    public string? ExtractionMethod { get; init; } // JsonLd | Heuristic | Llm
    public string? LicenseHint { get; init; }      // best-effort
}
```

**URL normalization for hashing:**
- Lowercase scheme and host
- Remove trailing slashes
- Sort query parameters
- Remove tracking parameters (utm_*, fbclid, etc.)

**Hash encoding:** Uses **base64url** (RFC 4648 §5) to avoid `/` and `+` characters that cause issues in URLs, log contexts, and storage keys.

Add to `Recipe`:
```csharp
public RecipeSource? Source { get; init; }
```

### 6.2 Define `RecipeDraft`

`RecipeDraft` is the unit of review. It wraps the canonical `Recipe` payload with additional operational metadata:

```csharp
public record RecipeDraft
{
    public required Recipe Recipe { get; init; }
    public required RecipeSource Source { get; init; }
    public required ValidationReport Validation { get; init; }
    public SimilarityReport? Similarity { get; init; } // paraphrase guardrail output
    public List<ArtifactRef> Artifacts { get; init; } = new();
}
```

`RecipeDraft` is returned in task results and used in the UI review flow. Persisted documents remain canonical `Recipe` records (with optional `Source`).

**Provenance during review vs commit:**
- **During review:** Provenance lives in `RecipeDraft.Source`. The nested `RecipeDraft.Recipe.Source` is `null`.
- **On commit:** The server copies `RecipeDraft.Source` into `Recipe.Source` before persisting to Cosmos.
- This separation keeps the draft wrapper distinct from the canonical model while ensuring provenance is always preserved on commit.

### 6.3 Supporting Types

```csharp
/// <summary>
/// Validation output from schema and business rule checks.
/// </summary>
public record ValidationReport
{
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Output from the paraphrase similarity guardrail.
/// </summary>
public record SimilarityReport
{
    public int MaxContiguousTokenOverlap { get; init; }
    public double MaxNgramSimilarity { get; init; }
    public bool ViolatesPolicy { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// Reference to an artifact stored in Blob storage.
/// </summary>
public record ArtifactRef
{
    public required string Type { get; init; }  // e.g., "snapshot.text", "draft.recipe.json"
    public required string Uri { get; init; }   // e.g., "artifacts/{taskId}/snapshot.txt"
}
```

### 6.4 Prompt Registry models

Create a prompt registry for versioned templates:

```csharp
public record PromptTemplate
{
    public required string Id { get; init; }           // e.g., ingest.extract.v1
    public required string Name { get; init; }         // "Ingest Extract"
    public required string Phase { get; init; }        // Ingest.Extract, Ingest.Discover, Ingest.Normalize
    public required int Version { get; init; }
    public bool IsActive { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPromptTemplate { get; init; } // contains variables, e.g., {{url}}, {{content}}
    public Dictionary<string, string> Constraints { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}
```

### 6.5 Storage additions

Add a Cosmos container (recommended):
- `prompts` partition key `/phase` (enables efficient queries by phase, the primary access pattern)

**API implications:**
- `GET /api/prompts?phase=Ingest.Extract` → efficient partition-scoped query
- `GET /api/prompts/{id}` → requires `phase` query parameter for efficient point read: `GET /api/prompts/{id}?phase=Ingest.Extract`
- Cross-partition query by `id` alone is supported but less efficient (acceptable at small scale)

Optional (later):
- `recipe_imports` partition key `/taskId` (for richer analytics/auditing beyond TaskState)

### 6.6 Prompt Rendering (Scriban)

Prompt templates are rendered using **Scriban**.

Rendering rules:
- Template syntax: Scriban (variables referenced as `{{ url }}`, `{{ content }}`, etc.).
- Required variables for ingest phases: `url` (when applicable), `content`, `schema`.
  - Missing required variables → fail the phase with a structured error.
- Optional variables → render as empty strings unless otherwise specified.
- `content` truncation:
  - Sanitize first, then truncate to a configured character budget (default: **60,000 chars**).
  - If truncation is required, apply "importance trimming" first (prefer headings and list blocks; prioritize sections containing ingredients/instructions).
- Rendering is performed by an `IPromptRenderer` abstraction to allow swapping engines if needed, but Scriban is the supported default.

```csharp
/// <summary>
/// Abstraction for prompt template rendering.
/// </summary>
public interface IPromptRenderer
{
    /// <summary>
    /// Renders a template with the provided variables.
    /// </summary>
    /// <param name="template">The template string (Scriban syntax).</param>
    /// <param name="variables">Variable values to substitute.</param>
    /// <returns>The rendered prompt text.</returns>
    /// <exception cref="PromptRenderException">Thrown when required variables are missing.</exception>
    string Render(string template, IDictionary<string, object?> variables);
}

/// <summary>
/// Thrown when prompt rendering fails due to missing required variables or syntax errors.
/// </summary>
public class PromptRenderException : Exception
{
    public IReadOnlyList<string> MissingVariables { get; init; } = [];
    public PromptRenderException(string message) : base(message) { }
    public PromptRenderException(string message, IReadOnlyList<string> missingVariables) 
        : base(message) => MissingVariables = missingVariables;
}
```

---

## 7. Task Contracts

### 7.1 Creating ingest tasks

Use the existing task endpoint:
- `POST /api/tasks`

Set `AgentType = "Ingest"` and `Payload` as JSON.

**ThreadId:** A `ThreadId` is **required** for all ingest tasks. It groups related tasks (e.g., multiple import attempts, normalization follow-ups) and enables session-scoped artifact discovery. Multiple ingest tasks may share the same `ThreadId` for grouped imports within a user session.

- **UI behavior:** The UI generates a GUID per "import session" and reuses it for retries and normalization follow-ups.
- **Gateway enforcement:** If `ThreadId` is missing, Gateway generates one and returns it in the `POST /api/tasks` response so the UI can continue using it.

#### Payload: Import by Query
```json
{
  "mode": "query",
  "query": "best tonkotsu ramen",
  "constraints": {
    "dietType": null,
    "cuisine": "Japanese"
  },
  "promptIds": {
    "extract": "ingest.extract.v1",
    "normalize": "ingest.normalize.v1"
  }
}
```

> **Note:** `promptIds.discover` is omitted because `Ingest.Discover` uses `ISearchProvider` by default (not LLM-backed). Include `"discover": "ingest.discover.v1"` only when the optional LLM browsing provider is enabled.

#### Payload: Import by URL
```json
{
  "mode": "url",
  "url": "https://example.com/recipes/tonkotsu-ramen",
  "promptIds": {
    "extract": "ingest.extract.v1",
    "normalize": "ingest.normalize.v1"
  }
}
```

#### Payload: Normalize Stored Recipe
```json
{
  "mode": "normalize",
  "recipeId": "existing-recipe-id-from-cosmos",
  "promptIds": {
    "normalize": "ingest.normalize.v1"
  }
}
```

#### Task Metadata (recommended)
- `promptId:*` copied into `AgentTask.Metadata` for auditability
- `requestedBy` (user id when available)

### 7.2 Task Results

#### Result: Ingest (ReviewReady)

The result payload uses the full `RecipeDraft` structure:

```json
{
  "draft": {
    "recipe": {
      "id": null,
      "name": "Tonkotsu Ramen",
      "description": "Rich, creamy pork bone broth ramen...",
      "ingredients": [...],
      "instructions": [...],
      "cuisine": "Japanese",
      "dietType": null,
      "prepTimeMinutes": 30,
      "cookTimeMinutes": 720,
      "servings": 4,
      "nutrition": null,
      "tags": ["ramen", "japanese", "soup"],
      "imageUrl": null,
      "source": null
    },
    "source": {
      "url": "https://example.com/recipes/tonkotsu-ramen",
      "siteName": "Example Recipes",
      "author": "Chef Example",
      "retrievedAt": "2025-12-13T14:20:00Z",
      "extractionMethod": "JsonLd",
      "licenseHint": null
    },
    "validation": {
      "errors": [],
      "warnings": [
        "PrepTimeMinutes missing; defaulted to 0",
        "Ingredient quantities partially unparsed; review recommended"
      ],
      "isValid": true
    },
    "similarity": {
      "maxContiguousTokenOverlap": 12,
      "maxNgramSimilarity": 0.08,
      "violatesPolicy": false,
      "details": null
    },
    "artifacts": [
      { "type": "snapshot.text", "uri": "artifacts/{taskId}/snapshot.txt" },
      { "type": "jsonld.recipe", "uri": "artifacts/{taskId}/recipe.jsonld" },
      { "type": "draft.recipe.json", "uri": "artifacts/{taskId}/draft.recipe.json" }
    ]
  },
  "candidates": [
    { "url": "https://example.com/recipes/tonkotsu-ramen", "title": "Tonkotsu Ramen", "score": 0.95 },
    { "url": "https://other.com/ramen", "title": "Easy Ramen", "score": 0.82 }
  ],
  "selectedCandidateIndex": 0
}
```

#### Result: Normalize

Each patch operation includes a `riskCategory` to support future auto-apply for low-risk changes:

```json
{
  "patch": [
    { "op": "replace", "path": "/ingredients/0/unit", "value": "tbsp", "riskCategory": "Low" },
    { "op": "replace", "path": "/ingredients/2/name", "value": "green onions", "riskCategory": "High" }
  ],
  "diffSummary": [
    "Normalized units: tablespoon -> tbsp (Low)",
    "Standardized ingredient name: scallions -> green onions (High)"
  ],
  "validation": { "errors": [], "warnings": [], "isValid": true }
}
```

Risk categories:
- **Low:** unit abbreviation, whitespace/casing, numeric/temperature formatting
- **High:** ingredient renaming, unit conversion, quantity changes, step reordering

---

## 8. Gateway API Extensions

### 8.0 AgentType Validation

Gateway MUST validate `AgentType` before enqueuing tasks. Unknown agent types are rejected with `400 Bad Request`.

- Valid values (v1): `Ingest`, `Research`, `Analysis`
- Error body includes: `code`, `message`, and the invalid `agentType`.

```csharp
/// <summary>
/// Known agent types supported by the platform.
/// </summary>
public static class KnownAgentTypes
{
    public const string Ingest = "Ingest";
    public const string Research = "Research";
    public const string Analysis = "Analysis";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Ingest,
        Research,
        Analysis
    };

    public static bool IsValid(string agentType) => All.Contains(agentType);
}
```

### 8.0.1 Create Ingest Task Contract

`POST /api/tasks` is used to enqueue ingest work. The Gateway persists the `AgentTask`, initializes `TaskState`, and enqueues into the Redis stream for `AgentType=Ingest`.

**Request (query import):**
```json
{
  "agentType": "Ingest",
  "threadId": "thread-123",
  "payload": {
    "mode": "Query",
    "query": "tonkotsu ramen with chashu",
    "constraints": {
      "diet": null,
      "cuisine": "Japanese",
      "maxPrepMinutes": 60
    },
    "promptSelection": {
      "discoverPromptId": null,
      "extractPromptId": null,
      "normalizePromptId": null
    },
    "promptOverrides": {
      "discoverOverride": null,
      "extractOverride": null,
      "normalizeOverride": null
    }
  }
}
```

**Request (URL import):**
```json
{
  "agentType": "Ingest",
  "threadId": "thread-123",
  "payload": {
    "mode": "Url",
    "url": "https://example.com/recipes/tonkotsu-ramen",
    "promptSelection": {
      "extractPromptId": null,
      "normalizePromptId": null
    },
    "promptOverrides": {
      "extractOverride": null,
      "normalizeOverride": null
    }
  }
}
```

**Response:**
```json
{
  "taskId": "task-abc123",
  "threadId": "thread-123",
  "agentType": "Ingest",
  "status": "Pending"
}
```

**Notes:**
- The Gateway stores `payload` as JSON (string) in `AgentTask.Payload` to match the existing `AgentTask` model.
- `promptOverrides` (if used) apply **only to the created task** and are recorded in task metadata for audit; prompt publishing remains admin-only per Section 8.1.
- Unknown `agentType` → `400 INVALID_AGENT_TYPE` (see Section 8.0).
- If `threadId` is omitted, the Gateway generates a new one and returns it in the response.
- `promptSelection` fields default to the active prompt for each phase if `null`.

### 8.1 Prompt Registry APIs (control plane)

These are management endpoints (not MCP tools) and require admin authorization.

- `GET /api/prompts?phase=Ingest.Extract`
- `GET /api/prompts/{id}`
- `POST /api/prompts` (create new version)
- `POST /api/prompts/{id}/activate`
- `POST /api/prompts/{id}/deactivate`

### 8.2 Ingest Commit API

To keep the existing `POST /api/recipes` contract stable, introduce a dedicated commit endpoint (recommended) that:
- attaches provenance
- stores import artifacts metadata
- optionally records import events

Options:
1) **Reuse** `POST /api/recipes` (simple), or
2) Add: `POST /api/recipes/import` (recommended for explicit semantics)

Suggested contract:
```json
{
  "draftRecipe": { /* canonical Recipe */ },
  "source": { /* RecipeSource */ },
  "taskId": "..."
}
```

Server behavior:
- Validate canonical recipe
- Assign `Id` if missing
- Set `CreatedAt` / `UpdatedAt`
- Persist to Cosmos `recipes`

#### Duplicate URL Handling

**v1 behavior:** Duplicate source URLs are **allowed**. The commit endpoint does not check for existing recipes with the same `Source.Url`.

- Rationale: The same source may be imported multiple times intentionally (e.g., to capture updates, or by different users).
- A warning is added to `ValidationReport.Warnings` if a recipe with the same `Source.Url` already exists.
- Future enhancement: optional deduplication with merge/replace semantics.

#### Error Response Structure

All error responses from ingest-related endpoints follow a consistent structure:

```json
{
  "code": "DRAFT_EXPIRED",
  "message": "Draft expired and cannot be committed.",
  "details": {
    "taskId": "abc-123",
    "expiredAt": "2025-06-20T00:00:00Z"
  }
}
```

Common error codes:
| Code | HTTP Status | Description |
|------|-------------|-------------|
| `INVALID_AGENT_TYPE` | 400 | Unknown `AgentType` value |
| `INVALID_PAYLOAD` | 400 | Payload validation failed |
| `TASK_NOT_FOUND` | 404 | Task does not exist |
| `DRAFT_EXPIRED` | 410 | Draft has expired |
| `ALREADY_COMMITTED` | 200 | Idempotent success (already committed) |
| `COMMIT_CONFLICT` | 409 | Concurrent commit detected |

---

## 9. MCP Server Tooling (tool plane)

Expose read-only tools to enable agent workflows to retrieve/render prompt templates and to support ingestion phases.

### 9.1 Prompt tools (read-only)
- `prompt_list(phase?: string)` → returns list of prompts, optionally filtered by phase; each result includes `phase` for efficient follow-up calls
- `prompt_get(id: string, phase?: string)` → returns prompt by ID; include `phase` for efficient point read (cross-partition query if omitted)
- `prompt_get_active(phase: string)` → returns the currently active prompt for a phase
- `prompt_render(id: string, phase?: string, variables: object)` → returns composed prompt text after Scriban rendering

### 9.2 Optional ingestion tools
If you choose to let agents run the full ingest pipeline via MCP calls:
- `web_search(query, constraints)` → candidate URLs
- `web_fetch(url)` → sanitized text + metadata
- `recipe_extract(content, url)` → draft recipe JSON
- `recipe_validate(draftRecipe, sourceContentHash?)`
- `recipe_normalize(draftRecipe)` → patch

**Note:** Prompt editing/publishing must remain in Gateway/admin (not MCP) to reduce risk.

---

## 10. Content Acquisition and Sanitization

### 10.1 Fetching
- Only allow `http`/`https`.
- Enforce max response size (e.g., 2–5 MB).
- Block internal/private IP ranges (SSRF protection).
- Apply per-domain rate limiting and retry/backoff.
- **User-Agent:** Use a descriptive User-Agent header (e.g., `CookbookIngestAgent/1.0 (+https://cookbook.example.com/bot)`).
- **robots.txt:** Best-effort respect for `robots.txt` is recommended but not strictly enforced (operator-configurable via `IngestOptions.RespectRobotsTxt`).
- **Retry policy (v1):**
  - Retry up to **2** times on network timeouts, `5xx`, or `429` with exponential backoff.
  - Do not retry most `4xx` (except `408`/`429`).
  - Fail the task with a structured error if all retries are exhausted.

### 10.2 Sanitization
- Produce a "clean text snapshot" suitable for LLM input:
  - remove scripts/styles/nav
  - keep headings + lists when possible
  - preserve ordering for steps and ingredients

### 10.3 Structured-data preference
Extraction should attempt, in order:
1. Schema.org Recipe JSON-LD
2. Microdata (if present)
3. Heuristic DOM extraction
4. LLM extraction from sanitized text

Record `ExtractionMethod` in provenance.

---

## 11. Paraphrasing and "No Large Verbatim Blocks" Policy

### 11.1 Prompt requirement
All extraction prompts MUST include a policy clause:

- Do **not** output large verbatim text blocks from the source.
- If the source contains large blocks of text, **summarize and rephrase** while preserving cooking meaning.
- Keep factual details accurate (quantities, temperatures, times).
- Output must be valid JSON for the canonical schema (no extra text).

### 11.2 Server-side verification (guardrail)
Add a post-extraction similarity check between:
- sanitized source snapshot text
- `draftRecipe.Description` and `draftRecipe.Instructions[]`

**Tokenization:** For v1, tokens are defined as **whitespace-delimited words** (case-insensitive, punctuation stripped). This provides a simple, deterministic baseline without external tokenizer dependencies.

Minimum viable approach (v1; configurable via `IngestGuardrailOptions`):
- **Contiguous token overlap** on sliding windows:
  - Warning if any overlap window ≥ **40 tokens**
  - Error if any overlap window ≥ **80 tokens**
- **5-gram Jaccard similarity** (instruction step vs source windows):
  - Warning if ≥ **0.20**
  - Error if ≥ **0.35**

If flagged:
- mark validation warning/error
- optionally run an "LLM repair prompt" phase: `Ingest.RepairParaphrase`

### 11.3 Guardrail Configuration

```csharp
/// <summary>
/// Configuration options for the paraphrase/similarity guardrail.
/// Bind via: services.Configure&lt;IngestGuardrailOptions&gt;(config.GetSection("Ingest:Guardrail"))
/// </summary>
public class IngestGuardrailOptions
{
    /// <summary>
    /// Contiguous token overlap threshold that triggers a warning.
    /// </summary>
    public int TokenOverlapWarningThreshold { get; set; } = 40;

    /// <summary>
    /// Contiguous token overlap threshold that triggers an error.
    /// </summary>
    public int TokenOverlapErrorThreshold { get; set; } = 80;

    /// <summary>
    /// N-gram Jaccard similarity threshold that triggers a warning.
    /// </summary>
    public double NgramSimilarityWarningThreshold { get; set; } = 0.20;

    /// <summary>
    /// N-gram Jaccard similarity threshold that triggers an error.
    /// </summary>
    public double NgramSimilarityErrorThreshold { get; set; } = 0.35;

    /// <summary>
    /// The size of n-grams used for similarity detection.
    /// </summary>
    public int NgramSize { get; set; } = 5;

    /// <summary>
    /// Whether to automatically trigger the RepairParaphrase phase on error.
    /// </summary>
    public bool AutoRepairOnError { get; set; } = true;
}
```

**Configuration binding:**
```csharp
// In Program.cs or service registration
services.Configure<IngestOptions>(config.GetSection("Ingest"));
services.Configure<IngestGuardrailOptions>(config.GetSection("Ingest:Guardrail"));
```

---

## 12. Validation Rules

### 12.1 Schema validation
- `Recipe.Name` required
- at least 1 ingredient and 1 instruction recommended (warning if missing)
- `PrepTimeMinutes`, `CookTimeMinutes`, `Servings`:
  - allow 0 but warn if absent/unknown
- ingredient line handling:
  - if parsing confidence low, store full line in `Name` and set `Quantity=0`, `Unit=null`, and use `Notes` for hints

**Invalid JSON / schema handling:**
- If LLM output fails JSON parsing or schema validation, run up to **2** repair attempts in `Ingest.RepairJson`.
- If still invalid after repairs, fail the task and preserve artifacts for inspection.

### 12.2 Business validation
- Detect unrealistic values (e.g., prep time > 24h) and warn.
- Detect duplicate steps and warn.
- Detect missing temperatures for baking recipes heuristically and warn.

### 12.3 Normalization Risk Categories (v1)

By default, **all normalization patches require human approval**.

- **Low-risk (candidates for future auto-apply behind a feature flag):**
  - unit abbreviation standardization (e.g., `tablespoon` → `tbsp`)
  - whitespace/casing normalization
  - numeric formatting (without unit conversion)
  - temperature formatting (e.g., `350 F` → `350°F`)
- **High-risk (always requires approval):**
  - ingredient renaming/canonicalization (e.g., `scallions` → `green onions`)
  - unit conversion (oz ↔ g, cups ↔ ml)
  - quantity changes
  - step reordering/merging/splitting

---

## 13. Messaging and Real-Time Updates

Reuse existing TaskState patterns and extend event types.

### 13.1 TaskState updates
- `TaskState.CurrentPhase` transitions through Ingest phases
- `Progress` moves 0..100 (mapped by phase weights)
- `Result` contains final JSON result for ReviewReady/Normalize

#### Phase Progress Weights (default)

Progress is calculated by summing completed phase weights:

| Phase | Weight | Cumulative |
|-------|--------|------------|
| `Ingest.Discover` | 10% | 10% |
| `Ingest.Fetch` | 15% | 25% |
| `Ingest.Extract` | 40% | 65% |
| `Ingest.Validate` | 25% | 90% |
| `Ingest.ReviewReady` | 10% | 100% |

For URL imports (no discovery), weights are redistributed proportionally.

### 13.2 SignalR Events
Add new event types:
- `ingest.progress`
- `ingest.discover.progress`
- `ingest.fetch.progress`
- `ingest.extract.progress`
- `ingest.validate.progress`
- `ingest.review_ready`
- `ingest.normalize.progress`
- `ingest.normalize.completed`

---

## 14. Artifacts

Store artifacts in Blob (`artifacts` container) under `{taskId}/{filename}`.

Recommended artifact set:
- `snapshot.txt` (sanitized text)
- `page.meta.json` (status code, content-type, retrieval timestamp, size)
- `recipe.jsonld` (if extracted)
- `draft.recipe.json` (canonical draft)
- `candidates.json` (discovery candidates list)
- `normalize.patch.json` (if normalization run)
- `normalize.diff.md` (human-readable diff summary)

Artifact metadata is discoverable via `GET /api/tasks/{taskId}/artifacts`.

### 14.1 Retention Policy

Default retention (operator-configurable via Blob lifecycle policies):

- **Committed** imports: retain artifacts for **180 days**
- **Rejected / Expired / Failed**: retain artifacts for **30 days**, then delete

Artifacts SHOULD be sanitized (text snapshots, extracted JSON-LD) and avoid storing large raw HTML unless explicitly enabled.

---

## 15. Security, Safety, and Operational Controls

### 15.1 SSRF and URL Safety
- Block private ranges, localhost, link-local
- Enforce allowlist of schemes
- Enforce DNS re-resolution and IP pinning checks
- Use a dedicated outbound HTTP client with strict timeouts

### 15.2 Operator Controls
- Domain allow/deny lists
- Rate limit configuration per domain
- Max concurrent fetches (global) and per-domain caps
- **Circuit breaker:** Domains with consistent failures (4xx/anti-bot responses) are temporarily blocked:
  - After **5 consecutive failures** within **10 minutes**, block domain for **30 minutes**
  - Configurable via `IngestOptions.CircuitBreaker.*`

### 15.3 Authorization
- Prompt management endpoints require admin role.
- Ingest tasks require authenticated session (if/when auth exists).
- MCP prompt tools are read-only.

### 15.4 Artifact Size Limits
- Individual artifacts capped at `IngestOptions.MaxArtifactSizeBytes` (default: 1 MB)
- Larger content is truncated or compressed (gzip for text snapshots)
- Raw HTML storage is disabled by default; only sanitized snapshots are stored

---

## 16. Observability and Telemetry

Minimum required metrics:
- ingest tasks created/completed/failed
- phase durations (discover/fetch/extract/validate/normalize)
- extraction method distribution (JsonLd vs Llm)
- paraphrase violations flagged
- average token usage per phase (if available from LLM provider)

Logs:
- include `taskId`, `threadId`, `phase`, and `sourceUrl` (careful: do not log full page content)

Tracing:
- propagate correlation IDs from Gateway → Orchestrator → Agents

---

## 17. Testing Strategy (xUnit)

### 17.1 Unit tests
- Payload parsing → phase routing
- JSON-LD extraction parser
- sanitization pipeline (fixture HTML → snapshot)
- similarity guardrail detection
- schema validation rules

### 17.2 Integration tests
- End-to-end ingest task with deterministic fixtures (stored HTML/JSON-LD)
- Artifact storage and retrieval
- Commit endpoint persists to Cosmos emulator

### 17.3 Golden set evaluation (recommended)
Maintain a curated list of recipe fixtures (URL snapshots, expected canonical outputs) to detect regressions when prompt versions change.

---

## 18. Rollout Plan

1. **Phase 0:** Implement data models + prompt registry (read-only retrieval).
2. **Phase 1:** URL import (fetch → extract → validate → review).
3. **Phase 2:** Query discovery (web search provider + candidate ranking).
4. **Phase 3:** Normalization + diff workflow.
5. **Phase 4:** Guardrail repair loops and expanded evaluation harness.

---

## 19. Open Questions

1. Which **Search API provider** will be implemented first for `ISearchProvider` (Bing vs Google Custom Search vs curated sources)?
2. Should the optional **LLM browsing** discovery provider be supported in v1 (behind a feature flag), or deferred?
3. Should "import transcript" notes be stored in `notes` (thread-scoped) or as task artifacts only?

---

## 20. Appendix: Example Prompt Templates

### 20.1 Ingest.Extract (v1) — User Prompt Template (example)

Variables:
- `{{url}}`
- `{{content}}` (sanitized page text, bounded length)
- `{{schema}}` (JSON schema excerpt or type signature)

Template (illustrative):
- Extract a recipe from the content for URL {{url}}.
- Prefer structured recipe data if present.
- Output **only** valid JSON for the canonical Recipe schema.
- **Do not include large verbatim blocks.** If the content contains long passages, **rephrase/summarize** while preserving cooking meaning (quantities, times, temperatures, key techniques).
- If an ingredient line cannot be confidently parsed, keep the full line in `Ingredient.Name` and set `Quantity=0`, `Unit=null`.
- Provide best-effort values for Cuisine, DietType, PrepTimeMinutes, CookTimeMinutes, Servings; use 0 if unknown.

---

### 20.2 Ingest.Normalize (v1) — Policy
- Normalize units and ingredient names while preserving meaning.
- Output a JSON Patch array (RFC 6902 style) only.
- No large verbatim blocks; keep descriptions concise.

---

## 21. Appendix: Configuration Summary

All configuration options introduced by this feature:

| Option Class | Section | Key Settings |
|--------------|---------|--------------|
| `IngestOptions` | `Ingest` | `MaxDiscoveryCandidates`, `DraftExpirationDays`, `MaxFetchSizeBytes`, `ContentCharacterBudget`, `RespectRobotsTxt`, `MaxArtifactSizeBytes`, `CircuitBreaker` |
| `CircuitBreakerOptions` | `Ingest:CircuitBreaker` | `FailureThreshold`, `FailureWindowMinutes`, `BlockDurationMinutes` |
| `IngestGuardrailOptions` | `Ingest:Guardrail` | `TokenOverlapWarningThreshold`, `TokenOverlapErrorThreshold`, `NgramSimilarityWarningThreshold`, `NgramSimilarityErrorThreshold`, `NgramSize`, `AutoRepairOnError` |
| `LlmRouterOptions.PhaseProviders` | `Llm:PhaseProviders` | `Ingest.Extract`, `Ingest.Normalize`, `Ingest.RepairJson`, `Ingest.RepairParaphrase` |

**DI Registration:**
```csharp
// In Program.cs or service registration
services.Configure<IngestOptions>(config.GetSection("Ingest"));
services.Configure<IngestGuardrailOptions>(config.GetSection("Ingest:Guardrail"));
// CircuitBreakerOptions is nested in IngestOptions, no separate registration needed
```

Example `appsettings.json` fragment:

```json
{
  "Ingest": {
    "MaxDiscoveryCandidates": 10,
    "DraftExpirationDays": 7,
    "MaxFetchSizeBytes": 5242880,
    "ContentCharacterBudget": 60000,
    "RespectRobotsTxt": true,
    "MaxArtifactSizeBytes": 1048576,
    "Guardrail": {
      "TokenOverlapWarningThreshold": 40,
      "TokenOverlapErrorThreshold": 80,
      "NgramSimilarityWarningThreshold": 0.20,
      "NgramSimilarityErrorThreshold": 0.35,
      "NgramSize": 5,
      "AutoRepairOnError": true
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "FailureWindowMinutes": 10,
      "BlockDurationMinutes": 30
    }
  },
  "Llm": {
    "PhaseProviders": {
      "Ingest.Extract": "OpenAI",
      "Ingest.Normalize": "OpenAI",
      "Ingest.RepairJson": "OpenAI",
      "Ingest.RepairParaphrase": "OpenAI"
    }
  }
}
