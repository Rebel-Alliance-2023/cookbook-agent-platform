# Implementation Plan A: Recipe Ingest Agent (Vertical Slice MVP)

**Applies to:** *Feature Specification – Recipe Ingest Agent* (final)  
**Target stack:** .NET 10, Aspire-orchestrated microservices, Cosmos DB + Blob Storage, Redis Streams + SignalR

---

## 1. Goals

Deliver an end-to-end, production-credible workflow to:

1. Import a recipe from a public web URL into the platform as a **reviewable draft** (`RecipeDraft`).
2. Enforce **human-in-the-loop** approval before creating a persisted canonical `Recipe` in Cosmos DB.
3. Preserve **provenance** (source URL, retrieval timestamp, extraction method) and enforce a **"no large verbatim blocks"** paraphrasing policy.
4. Support an optional **Normalization** pass (patch/diff) without blocking initial ingestion.

---

## 2. Guiding Principles

- **Vertical slice first:** ship a complete, minimal URL import path before adding discovery or normalization.
- **Deterministic boundaries:** prefer structured data (JSON-LD) and deterministic parsing where possible; use LLMs for extraction/repair.
- **Operator-configurable:** prompts, thresholds, provider routing, and retention are configuration-first.
- **Auditability:** store artifacts (sanitized snapshots, JSON-LD, draft JSON, repair outputs) in Blob with retention policies.
- **Safe by default:** SSRF protections, rate limits, size limits, and similarity guardrails.

---

## 3. Deliverables by Milestone

### Milestone 0 — Foundation: Models, Options, Contracts, and Prompt Registry

**Objective:** Establish the types, config bindings, storage, and internal contracts required by the ingest pipeline.

**Deliverables**

#### Domain Models (`Cookbook.Platform.Shared`)
- `RecipeSource` record with `Url`, `UrlHash`, `SiteName`, `Author`, `RetrievedAt`, `ExtractionMethod`, `LicenseHint`
- Add optional `Source` property to existing `Recipe` model
- `RecipeDraft` wrapper record (includes `Recipe`, `Source`, `ValidationReport`, optional `SimilarityReport`, `Artifacts[]`)
- `ValidationReport` record (errors/warnings lists + `IsValid` computed property)
- `SimilarityReport` record (token overlap, n-gram similarity, policy violation flag)
- `ArtifactRef` record (type + URI)

#### URL Normalization Utilities
- `UrlNormalizer` class: lowercase scheme/host, remove trailing slashes, sort query params, strip tracking params
- `UrlHasher` class: compute base64url-encoded SHA256 hash (first 22 chars) for duplicate detection

#### Configuration Options
- `IngestOptions` with all spec properties including nested `CircuitBreakerOptions`
- `IngestGuardrailOptions` (thresholds for verbatim checks)
- DI registration helpers for `services.Configure<T>()`

#### Prompt Registry Infrastructure
- Cosmos container `prompts` (partition key `/phase`)
- `PromptTemplate` model per spec Section 6.4
- `IPromptRenderer` interface + `ScribanPromptRenderer` implementation
- `PromptRenderException` for missing required variables
- Gateway admin APIs:
  - `GET /api/prompts?phase=...`
  - `GET /api/prompts/{id}?phase=...`
  - `POST /api/prompts`
  - `POST /api/prompts/{id}/activate`
  - `POST /api/prompts/{id}/deactivate`
- Seed initial `ingest.extract.v1` prompt template

#### Agent Type Validation
- `KnownAgentTypes` static class with `Ingest`, `Research`, `Analysis`
- Gateway validation middleware rejecting unknown types with `400 INVALID_AGENT_TYPE`

**Acceptance Criteria**
- Unit tests prove new models serialize/deserialize cleanly (System.Text.Json + Newtonsoft for Cosmos compatibility)
- `UrlHasher` produces consistent, URL-safe hashes
- Options bind correctly from `appsettings.json` (including nested `Ingest:Guardrail` and `Ingest:CircuitBreaker`)
- Gateway rejects unknown `agentType` values with structured error response
- Prompt CRUD APIs work against Cosmos emulator
- Scriban renderer handles required/optional variables and truncation correctly

---

### Milestone 1 — URL Import Vertical Slice: Fetch → Extract → Validate → ReviewReady

**Objective:** Provide a working URL import experience that produces a valid `RecipeDraft` and artifacts, visible in UI.

**Deliverables**

#### Gateway: Create Ingest Task Contract
- Implement `POST /api/tasks` for `AgentType=Ingest` with request contract:
  ```json
  {
    "agentType": "Ingest",
    "threadId": "...",
    "payload": {
      "mode": "Url",
      "url": "https://...",
      "promptSelection": { "extractPromptId": null, "normalizePromptId": null },
      "promptOverrides": { "extractOverride": null, "normalizeOverride": null }
    }
  }
  ```
- Response contract: `{ taskId, threadId, agentType, status }`
- **ThreadId generation:** If `threadId` is missing, Gateway generates a GUID and returns it

#### Orchestrator: Ingest Phase Runner
- Phase pipeline: `Ingest.Fetch` → `Ingest.Extract` → `Ingest.Validate` → `Ingest.ReviewReady`
- TaskState updates + progress messages (Redis Streams → SignalR)
- Progress weights per spec: Fetch=15%, Extract=40%, Validate=25%, ReviewReady=10% (redistributed without Discover)

#### Fetch + Sanitization Service
- Allow only `http`/`https` schemes
- Enforce `IngestOptions.MaxFetchSizeBytes` (default 5 MB)
- SSRF protection:
  - Block private IP ranges (10.x, 172.16-31.x, 192.168.x, 127.x, ::1, link-local)
  - Verify resolved IPs before connection
- User-Agent header: `CookbookIngestAgent/1.0 (+https://...)`
- Per-domain rate limiting with retry/backoff (2 retries, exponential backoff)
- **Circuit breaker:** Track failures per domain; block after 5 failures in 10 min for 30 min
- Optional robots.txt respect (configurable)
- Produce sanitized text snapshot (remove scripts/styles/nav, preserve headings/lists)

#### Structured Data Extraction
- Detect and parse Schema.org Recipe JSON-LD if present
- If JSON-LD absent/insufficient: fall back to LLM extraction
- Record `ExtractionMethod` in `RecipeSource` (`JsonLd` | `Heuristic` | `Llm`)

#### Prompt Rendering for Extraction
- Retrieve active prompt template for `Ingest.Extract`
- Render with Scriban: `{{ url }}`, `{{ content }}`, `{{ schema }}`
- Enforce content truncation budget (`IngestOptions.ContentCharacterBudget`, default 60K chars)
- Apply "importance trimming" (prioritize ingredient/instruction sections)

#### LLM Extraction
- Call configured LLM provider (via existing `LlmRouterOptions.PhaseProviders`)
- Require strict JSON output mapping to canonical `Recipe` fields
- **RepairJson loop:** If JSON parsing fails, retry up to 2 times with repair prompt

#### Artifacts
- Store to Blob (`artifacts/{taskId}/`):
  - `snapshot.txt` (sanitized text, respect `MaxArtifactSizeBytes`, gzip if over limit)
  - `page.meta.json` (url, retrievedAt, contentType, statusCode, length)
  - `recipe.jsonld` (when present)
  - `draft.recipe.json` (RecipeDraft output)
- Enforce `IngestOptions.MaxArtifactSizeBytes` (default 1 MB)

#### Blazor UI: URL Import Wizard
- New "Recipe Ingest Agent" entry point in navigation
- URL input form → task creation → streaming progress indicator
- ReviewReady view:
  - Display draft recipe fields (name, description, ingredients, instructions, times, servings)
  - Show validation warnings/errors
  - Display provenance (source URL, site name, retrieved at, extraction method)
  - Link to artifacts

**Acceptance Criteria**
- For a known good recipe URL (e.g., a site with JSON-LD), pipeline reaches `ReviewReady` with:
  - Populated `RecipeDraft` with all required fields
  - Correct `ExtractionMethod` in source
  - Blob artifacts stored and discoverable via `GET /api/tasks/{taskId}/artifacts`
  - UI displays draft and validation output
- For a URL without JSON-LD, LLM extraction produces valid draft
- If JSON parsing fails, RepairJson attempts are logged and artifacts stored
- If fetch fails (404/timeout/SSRF block), task transitions to `Failed` with structured error
- Circuit breaker blocks repeat requests to failing domains

---

### Milestone 2 — Commit + Lifecycle: ReviewReady → Commit / Reject / Expire

**Objective:** Make review actionable and safe: commit creates canonical `Recipe` in Cosmos; reject ends flow; drafts expire.

**Deliverables**

#### Commit Endpoint
- `POST /api/recipes/import` with request:
  ```json
  {
    "taskId": "...",
    "draftRecipe": { /* optional edits */ },
    "source": { /* optional overrides */ }
  }
  ```
- Validation:
  - Task exists and is in `ReviewReady` state
  - Task not expired (`lastUpdated + DraftExpirationDays < now` → `410 DRAFT_EXPIRED`)
- **Commit idempotency:** If already `Committed`, return `200 ALREADY_COMMITTED` with existing recipe ID
- **Optimistic concurrency:** Use Cosmos ETag on TaskState; return `409 COMMIT_CONFLICT` on race
- **Provenance copy:** Set `Recipe.Source` = `RecipeDraft.Source` (compute `UrlHash` if not present)
- **Duplicate URL warning:** Query by `UrlHash`; add warning to response if duplicate exists (don't block)
- Assign `Recipe.Id` if null, set `CreatedAt`/`UpdatedAt`
- Persist to Cosmos `recipes` container
- Update TaskState to `Committed`

#### Reject Endpoint
- `POST /api/tasks/{taskId}/reject`
- Mark task `Rejected`, prevent future commit
- Return `200 OK` with terminal state

#### Expiration Background Job
- Aspire-hosted `BackgroundService` or timer-based worker
- Query: `SELECT * FROM tasks WHERE status = 'ReviewReady' AND lastUpdated < @expirationThreshold`
- Transition matching tasks to `Expired` status
- Run interval: every 15 minutes (configurable)

#### UI Actions
- Commit button → calls import endpoint → shows success with recipe link or error
- Reject button → calls reject endpoint → shows terminal state
- Visual indicators for terminal states (Committed/Rejected/Expired/Failed)
- Prevent actions on terminal states

**Acceptance Criteria**
- Commit creates Cosmos `Recipe` with `Source` populated, `UrlHash` computed, timestamps set
- Second identical commit request returns `200` with same recipe ID (idempotent)
- Concurrent commit attempts: one succeeds, other gets `409`
- Expired tasks return `410` on commit attempt
- Reject transitions task to `Rejected` and blocks commit
- Expiration job correctly marks stale tasks

---

### Milestone 3 — Verbatim Guardrails + Repair Phases

**Objective:** Enforce the paraphrasing policy with measurable guardrails and automated repair attempts.

**Deliverables**

#### Similarity Detection Service
- **Tokenization:** Whitespace-delimited words, case-insensitive, punctuation stripped
- **Contiguous token overlap:** Sliding window detection
  - Warning threshold: 40 tokens (configurable)
  - Error threshold: 80 tokens (configurable)
- **5-gram Jaccard similarity:** Per instruction step vs source windows
  - Warning threshold: 0.20 (configurable)
  - Error threshold: 0.35 (configurable)
- Produce `SimilarityReport` with max values and `ViolatesPolicy` flag

#### Integration into Validate Phase
- Run similarity checks after extraction, before ReviewReady
- Add results to `ValidationReport` (warnings or errors based on thresholds)
- Store `similarity.json` artifact

#### RepairParaphrase Phase
- If `IngestGuardrailOptions.AutoRepairOnError` is true and similarity exceeds error threshold:
  - Trigger `Ingest.RepairParaphrase` phase
  - Prompt LLM to rephrase flagged sections
  - Re-run similarity check
  - Store `repair.paraphrase.json` artifact with before/after
- If still violating after repair: mark as error (blocks commit unless operator overrides)

#### RepairJson Phase (if not completed in M1)
- If LLM output fails JSON parsing or schema validation:
  - Trigger `Ingest.RepairJson` phase (up to 2 attempts)
  - Prompt LLM with error details to fix JSON
  - Store `repair.json.{attempt}.json` artifacts

#### UI Surfacing
- Show `SimilarityReport` in ReviewReady view (max overlap, max similarity, policy status)
- Indicate if repair was attempted and outcome
- If error threshold exceeded and repair failed: show blocking error with explanation

**Acceptance Criteria**
- Fixture with long copied text triggers guardrail warnings/errors
- Similarity thresholds are configurable and respected
- AutoRepair triggers when enabled and reduces similarity (or fails gracefully)
- Repair artifacts stored for audit
- UI clearly shows guardrail status and blocks commit when appropriate

---

### Milestone 4 — Query Discovery via ISearchProvider

**Objective:** Enable query-driven imports by finding candidate URLs via a deterministic search provider.

**Deliverables**

#### ISearchProvider Abstraction
```csharp
public interface ISearchProvider
{
    Task<IReadOnlyList<SearchCandidate>> SearchAsync(SearchRequest request, CancellationToken ct);
}
```

#### Bing Web Search Implementation (chosen for v1)
- `BingSearchProvider` implementing `ISearchProvider`
- Configuration: API key, endpoint, market
- Quota controls: max requests per day (configurable)
- Per-domain allow/deny lists (configurable)
- Rate limiting: max 3 requests/second

#### Discover Phase
- `Ingest.Discover` phase in orchestrator
- Store `candidates.json` artifact with ranked list (url, title, snippet, siteName, score)
- Auto-select top candidate and proceed to Fetch
- Store `selectedCandidateIndex` in task result

#### Gateway: Query Import Contract
- Extend `POST /api/tasks` payload for `mode: "Query"`:
  ```json
  {
    "mode": "Query",
    "query": "tonkotsu ramen recipe",
    "constraints": { "cuisine": "Japanese", "diet": null },
    "promptSelection": { ... }
  }
  ```

#### UI Additions
- Query input mode toggle (URL / Query)
- Query input form with optional constraints
- Candidate list display (after discovery, before commit)
- "Try different candidate" action: create new task with selected candidate URL

**Acceptance Criteria**
- Query import reaches ReviewReady for test queries
- `candidates.json` artifact contains ranked candidates with scores
- Domain denylist prevents fetch from blocked sites
- UI displays candidate list and allows alternate selection
- Rate limits and quota caps enforced

---

### Milestone 5 — Normalize Pass: Patch/Diff + Apply

**Objective:** Add optional normalization as a second phase that generates an RFC6902 patch with risk categories.

**Deliverables**

#### Normalize Phase
- `Ingest.Normalize` phase triggered by:
  - User action on ReviewReady draft, OR
  - `mode: "normalize"` task with `recipeId` for stored recipes
- Prompt LLM to produce JSON Patch with `riskCategory` per operation:
  ```json
  [
    { "op": "replace", "path": "/ingredients/0/unit", "value": "tbsp", "riskCategory": "Low" },
    { "op": "replace", "path": "/ingredients/2/name", "value": "green onions", "riskCategory": "High" }
  ]
  ```
- Validate patch applicability (test apply without persisting)
- Store `normalize.patch.json` and `normalize.diff.md` artifacts

#### Gateway: Normalize Stored Recipe Contract
- Support `mode: "normalize"` with `recipeId`:
  ```json
  {
    "mode": "normalize",
    "recipeId": "existing-recipe-id",
    "promptSelection": { "normalizePromptId": null }
  }
  ```
- Fetch existing recipe from Cosmos, run normalize, produce patch

#### Diff UX (Blazor)
- Show before/after comparison or diff summary
- Color-code by risk category (Low=green, High=orange)
- Actions: Apply Patch, Reject Patch

#### Apply Patch Endpoint
- `POST /api/recipes/{recipeId}/apply-patch`
- Request: `{ taskId, patch: [...] }`
- Validate patch, apply to recipe document, persist
- Update TaskState to `Committed` (or dedicated `PatchApplied` state)

#### Reject Patch
- Leave recipe unchanged
- Record decision in TaskState

**Acceptance Criteria**
- Normalize produces valid JSON Patch with risk categories
- UI shows clear diff with risk indicators
- Applying patch updates recipe correctly (no schema violations)
- Rejecting patch leaves recipe unchanged
- Normalize works for both drafts and stored recipes

---

## 4. Engineering Workstreams

### 4.1 Services and Components

#### Gateway (`Cookbook.Platform.Gateway`)
- AgentType validation middleware
- Create ingest task endpoint with full contract (Section 8.0.1)
- ThreadId generation if missing
- Prompt registry CRUD APIs
- Commit/reject endpoints with concurrency control
- Apply-patch endpoint (Milestone 5)

#### Orchestrator (`Cookbook.Platform.Orchestrator`)
- Ingest phase runner (Discover → Fetch → Extract → Validate → Repair* → ReviewReady)
- Phase progress calculation and eventing
- Fetch service with SSRF protection and circuit breaker
- Sanitization pipeline
- JSON-LD extraction
- LLM extraction with repair loops
- Similarity guardrail service
- Normalize phase
- Artifact writer with size limits

#### Blazor UI (`Cookbook.Platform.Client.Blazor`)
- Import wizard component (URL/Query modes)
- Streaming progress component
- ReviewReady view with validation/guardrail surfacing
- Commit/Reject controls
- Candidate list component
- Normalize diff viewer
- Terminal state indicators

#### MCP Server (`Cookbook.Platform.Mcp`)
- `prompt_list(phase?: string)` → returns prompts with phase for efficient follow-up
- `prompt_get(id: string, phase?: string)` → include phase for efficient point read
- `prompt_get_active(phase: string)` → active prompt for phase
- `prompt_render(id: string, phase?: string, variables: object)` → rendered prompt text

#### Infrastructure (`Cookbook.Platform.Infrastructure`)
- Cosmos container initialization for `prompts`
- Blob lifecycle policies for artifact retention
- Circuit breaker state storage (Redis or in-memory with TTL)

---

## 5. Testing Strategy

### Unit Tests
- `UrlNormalizer`: various URL formats, tracking param removal
- `UrlHasher`: consistent hashing, base64url encoding
- JSON-LD extraction parsing (valid, invalid, missing cases)
- `ScribanPromptRenderer`: required/optional variables, truncation, importance trimming
- Guardrail computations: token overlap, 5-gram Jaccard (fixtures with known values)
- JSON Patch application and validation

### Integration Tests
- Fetch pipeline with recorded HTTP fixtures (WireMock)
- Circuit breaker behavior: 5 failures → block → unblock after duration
- Full phase transitions and TaskState updates
- Commit idempotency: same request twice → same result
- Commit concurrency: parallel requests → one wins, one gets 409
- Commit after expiration → 410
- Prompt CRUD against Cosmos emulator

### Golden Set Evaluation
- Curate 10-15 recipe pages with expected outputs (shape-level assertions)
- Include sites with JSON-LD, without JSON-LD, with anti-bot measures
- Track regressions when prompt versions change
- Automate as part of CI (optional fixture refresh)

---

## 6. Observability and Operations

### Structured Logging
- Include `taskId`, `threadId`, `phase`, `sourceUrl` in all log entries
- Log phase transitions, durations, and outcomes
- Avoid logging full page content (truncate or hash)

### Metrics
- `ingest_tasks_created_total` (counter, by mode)
- `ingest_phase_duration_seconds` (histogram, by phase)
- `ingest_phase_failures_total` (counter, by phase, error_code)
- `ingest_extraction_method_total` (counter, by method: JsonLd/Heuristic/Llm)
- `ingest_guardrail_violations_total` (counter, by severity: warning/error)
- `ingest_repair_attempts_total` (counter, by type: json/paraphrase)
- `ingest_domain_circuit_breaker_trips_total` (counter, by domain)

### Artifact Retention
- Committed imports: 180 days
- Rejected/Expired/Failed: 30 days
- Implement via Blob lifecycle management policies

### Alerting (recommended)
- High failure rate for Fetch phase (may indicate anti-bot or network issues)
- Circuit breaker trips (domain reliability issues)
- High guardrail violation rate (may indicate prompt tuning needed)

---

## 7. Key Decisions

| Decision | Resolution |
|----------|------------|
| Search provider for Milestone 4 | **Bing Web Search** (simpler API, good recipe coverage) |
| LLM browsing discovery provider | **Defer** (not in v1; optional future enhancement) |
| Import transcript storage | **Artifacts first** (not notes; cleaner separation) |
| Prompt registry write path | **Include in Milestone 0** (needed for seed data and operator iteration) |
| RepairJson phase | **Include in Milestone 1** (critical for extraction reliability) |

---

## 8. Exit Criteria for v1 "Done"

v1 is considered "done" when:

- [x] URL import works end-to-end (Fetch → Extract → Validate → ReviewReady → Commit)
- [x] JSON repair loop handles malformed LLM output
- [x] Provenance is persisted with the recipe (`Recipe.Source` with `UrlHash`)
- [x] Guardrails detect and flag large verbatim blocks
- [x] Paraphrase repair attempts to fix violations (configurable auto-repair)
- [x] Commit is idempotent and handles concurrency correctly
- [x] Expiration job marks stale drafts
- [x] Artifacts are stored with size limits and retention rules
- [x] Prompt registry supports CRUD and active prompt selection
- [x] UI provides complete URL import workflow

**v1.1 additions (optional for initial release):**
- [ ] Query discovery with Bing Search
- [ ] Normalize pass with diff viewer
- [ ] Candidate selection UI
