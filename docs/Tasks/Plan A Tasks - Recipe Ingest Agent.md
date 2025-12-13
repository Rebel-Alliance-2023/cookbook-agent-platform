# Plan A Tasks: Recipe Ingest Agent

**Source:** Implementation Plan A - Recipe Ingest Agent.md  
**Spec:** Feature Specification - Recipe Ingest Agent.md

---

## Task Legend

- **Status:** `[ ]` Not Started | `[~]` In Progress | `[x]` Complete | `[-]` Blocked
- **Priority:** P0 (Critical) | P1 (High) | P2 (Medium) | P3 (Low)
- **Size:** XS (<2h) | S (2-4h) | M (4-8h) | L (1-2d) | XL (2-5d)
- **Dependencies:** Listed by task ID

---

## Milestone 0 — Foundation: Models, Options, Contracts, and Prompt Registry

### Domain Models

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-001 | Create `RecipeSource` record with `Url`, `UrlHash`, `SiteName`, `Author`, `RetrievedAt`, `ExtractionMethod`, `LicenseHint` | S | P0 | - | [ ] |
| M0-002 | Add optional `Source` property (`RecipeSource?`) to existing `Recipe` model | XS | P0 | M0-001 | [ ] |
| M0-003 | Create `ValidationReport` record with `Errors`, `Warnings` lists and `IsValid` computed property | S | P0 | - | [ ] |
| M0-004 | Create `SimilarityReport` record with `MaxContiguousTokenOverlap`, `MaxNgramSimilarity`, `ViolatesPolicy`, `Details` | S | P0 | - | [ ] |
| M0-005 | Create `ArtifactRef` record with `Type` and `Uri` properties | XS | P0 | - | [ ] |
| M0-006 | Create `RecipeDraft` record wrapping `Recipe`, `RecipeSource`, `ValidationReport`, optional `SimilarityReport`, `List<ArtifactRef>` | S | P0 | M0-001 to M0-005 | [ ] |
| M0-007 | Add JSON serialization attributes to all new models | S | P1 | M0-001 to M0-006 | [ ] |
| M0-008 | Write unit tests for model serialization round-trips | M | P1 | M0-007 | [ ] |

### URL Normalization Utilities

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-009 | Create `UrlNormalizer` class: lowercase scheme/host, remove trailing slashes, sort query params | M | P0 | - | [ ] |
| M0-010 | Add tracking parameter removal to `UrlNormalizer` (utm_*, fbclid, etc.) | S | P1 | M0-009 | [ ] |
| M0-011 | Create `UrlHasher` class: compute base64url-encoded SHA256 hash (first 22 chars) | S | P0 | M0-009 | [ ] |
| M0-012 | Write unit tests for `UrlNormalizer` | M | P1 | M0-009, M0-010 | [ ] |
| M0-013 | Write unit tests for `UrlHasher` | S | P1 | M0-011 | [ ] |

### Configuration Options

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-014 | Create `CircuitBreakerOptions` class | XS | P0 | - | [ ] |
| M0-015 | Create `IngestOptions` class with nested `CircuitBreakerOptions` | S | P0 | M0-014 | [ ] |
| M0-016 | Create `IngestGuardrailOptions` class | S | P0 | - | [ ] |
| M0-017 | Add DI registration extension `AddIngestOptions()` | S | P1 | M0-015, M0-016 | [ ] |
| M0-018 | Add `Ingest` section to `appsettings.json` | XS | P1 | M0-015 | [ ] |
| M0-019 | Write unit tests for options binding | S | P1 | M0-017, M0-018 | [ ] |

### Prompt Registry Infrastructure

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-020 | Create `PromptTemplate` model per spec | S | P0 | - | [ ] |
| M0-021 | Add Cosmos container `prompts` (partition key `/phase`) | M | P0 | M0-020 | [ ] |
| M0-022 | Create `IPromptRepository` interface | S | P0 | M0-020 | [ ] |
| M0-023 | Implement `CosmosPromptRepository` | L | P0 | M0-021, M0-022 | [ ] |
| M0-024 | Create `IPromptRenderer` interface | XS | P0 | - | [ ] |
| M0-025 | Create `PromptRenderException` | XS | P0 | - | [ ] |
| M0-026 | Implement `ScribanPromptRenderer` | M | P0 | M0-024, M0-025 | [ ] |
| M0-027 | Add content truncation logic to renderer | M | P1 | M0-026 | [ ] |
| M0-028 | Add importance trimming for truncation | M | P2 | M0-027 | [ ] |
| M0-029 | Write unit tests for `ScribanPromptRenderer` | M | P1 | M0-026, M0-027 | [ ] |

### Gateway: Prompt Registry APIs

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-030 | Create `PromptEndpoints` route group `/api/prompts` | S | P0 | M0-023 | [ ] |
| M0-031 | Implement `GET /api/prompts?phase=...` | S | P0 | M0-030 | [ ] |
| M0-032 | Implement `GET /api/prompts/{id}?phase=...` | S | P0 | M0-030 | [ ] |
| M0-033 | Implement `POST /api/prompts` | M | P0 | M0-030 | [ ] |
| M0-034 | Implement `POST /api/prompts/{id}/activate` | S | P0 | M0-030 | [ ] |
| M0-035 | Implement `POST /api/prompts/{id}/deactivate` | S | P0 | M0-030 | [ ] |
| M0-036 | Add admin authorization to prompt endpoints | S | P1 | M0-030 | [ ] |
| M0-037 | Write integration tests for prompt CRUD | L | P1 | M0-031 to M0-035 | [ ] |

### Seed Data & Agent Type Validation

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M0-038 | Create `ingest.extract.v1` prompt template content | M | P0 | - | [ ] |
| M0-039 | Add seed data initializer for prompt | S | P0 | M0-023, M0-038 | [ ] |
| M0-040 | Create `KnownAgentTypes` static class | XS | P0 | - | [ ] |
| M0-041 | Add AgentType validation to task creation | S | P0 | M0-040 | [ ] |
| M0-042 | Return `400 INVALID_AGENT_TYPE` for unknown types | S | P0 | M0-041 | [ ] |
| M0-043 | Write tests for agent type validation | S | P1 | M0-042 | [ ] |

---

## Milestone 1 — URL Import Vertical Slice

### Gateway: Create Ingest Task

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-001 | Create `IngestTaskPayload` model | S | P0 | M0-006 | [ ] |
| M1-002 | Create `CreateIngestTaskRequest` model | S | P0 | M1-001 | [ ] |
| M1-003 | Create `CreateTaskResponse` model | XS | P0 | - | [ ] |
| M1-004 | Update `POST /api/tasks` for `AgentType=Ingest` | M | P0 | M1-002, M1-003 | [ ] |
| M1-005 | Implement ThreadId generation if missing | S | P0 | M1-004 | [ ] |
| M1-006 | Store payload as JSON in `AgentTask.Payload` | S | P0 | M1-004 | [ ] |
| M1-007 | Write integration tests for task creation | M | P1 | M1-004 | [ ] |

### Orchestrator: Phase Runner

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-008 | Create `IngestPhaseRunner` class | L | P0 | M1-004 | [ ] |
| M1-009 | Implement phase pipeline (Fetch?Extract?Validate?ReviewReady) | L | P0 | M1-008 | [ ] |
| M1-010 | Add TaskState updates at each phase | M | P0 | M1-009 | [ ] |
| M1-011 | Implement progress calculation with weights | S | P1 | M1-010 | [ ] |
| M1-012 | Emit progress via Redis Streams | M | P0 | M1-011 | [ ] |
| M1-013 | Add error handling ? Failed state | M | P0 | M1-009 | [ ] |
| M1-014 | Register runner in DI | S | P0 | M1-008 | [ ] |

### Fetch Service

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-015 | Create `IFetchService` interface | S | P0 | - | [ ] |
| M1-016 | Create `FetchResult` record | S | P0 | - | [ ] |
| M1-017 | Implement `HttpFetchService` | L | P0 | M1-015, M0-015 | [ ] |
| M1-018 | Add scheme validation (http/https only) | S | P0 | M1-017 | [ ] |
| M1-019 | Add response size enforcement | M | P0 | M1-017 | [ ] |
| M1-020 | Implement SSRF protection (block private IPs) | M | P0 | M1-017 | [ ] |
| M1-021 | Add DNS resolution verification | M | P0 | M1-020 | [ ] |
| M1-022 | Set User-Agent header | XS | P1 | M1-017 | [ ] |
| M1-023 | Implement retry with exponential backoff | M | P1 | M1-017 | [ ] |
| M1-024 | Create `ICircuitBreakerService` interface | S | P0 | M0-014 | [ ] |
| M1-025 | Implement circuit breaker service | L | P0 | M1-024 | [ ] |
| M1-026 | Integrate circuit breaker into fetch | M | P0 | M1-025 | [ ] |
| M1-027 | Add optional robots.txt respect | M | P2 | M1-017 | [ ] |
| M1-028 | Write fetch integration tests (WireMock) | L | P1 | M1-017 to M1-026 | [ ] |
| M1-029 | Write SSRF protection unit tests | M | P1 | M1-020 | [ ] |
| M1-030 | Write circuit breaker tests | M | P1 | M1-025 | [ ] |

### Sanitization

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-031 | Create `ISanitizationService` interface | S | P0 | - | [ ] |
| M1-032 | Create `SanitizedContent` record | S | P0 | - | [ ] |
| M1-033 | Implement `HtmlSanitizationService` | L | P0 | M1-031 | [ ] |
| M1-034 | Add JSON-LD extraction from script tags | M | P0 | M1-033 | [ ] |
| M1-035 | Filter for Recipe JSON-LD | S | P1 | M1-034 | [ ] |
| M1-036 | Write sanitization unit tests | M | P1 | M1-033 | [ ] |
| M1-037 | Write JSON-LD extraction tests | M | P1 | M1-034 | [ ] |

### Extraction

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-038 | Create `IRecipeExtractor` interface | S | P0 | M0-006 | [ ] |
| M1-039 | Create `ExtractionResult` record | S | P0 | - | [ ] |
| M1-040 | Implement `JsonLdRecipeExtractor` | L | P0 | M1-038, M1-035 | [ ] |
| M1-041 | Map Schema.org Recipe to canonical model | L | P0 | M1-040 | [ ] |
| M1-042 | Handle ingredient parsing | M | P1 | M1-041 | [ ] |
| M1-043 | Write JSON-LD extraction tests | L | P1 | M1-040 | [ ] |
| M1-044 | Create `ILlmRecipeExtractor` interface | XS | P0 | M1-038 | [ ] |
| M1-045 | Implement `LlmRecipeExtractor` | L | P0 | M1-044, M0-026 | [ ] |
| M1-046 | Retrieve active prompt for Extract phase | M | P0 | M1-045, M0-023 | [ ] |
| M1-047 | Render prompt with Scriban | M | P0 | M1-046 | [ ] |
| M1-048 | Enforce content truncation | S | P0 | M1-047 | [ ] |
| M1-049 | Parse LLM JSON response | M | P0 | M1-045 | [ ] |
| M1-050 | Implement RepairJson loop (up to 2 retries) | L | P0 | M1-049 | [ ] |
| M1-051 | Store repair artifacts | S | P1 | M1-050 | [ ] |
| M1-052 | Write LLM extraction tests | L | P1 | M1-045 to M1-050 | [ ] |
| M1-053 | Create `RecipeExtractionOrchestrator` (JSON-LD ? LLM fallback) | M | P0 | M1-040, M1-045 | [ ] |
| M1-054 | Set ExtractionMethod in RecipeSource | S | P0 | M1-053 | [ ] |
| M1-055 | Populate RecipeDraft with results | M | P0 | M1-053 | [ ] |

### Validation

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-056 | Create `IRecipeValidator` interface | S | P0 | M0-003 | [ ] |
| M1-057 | Implement schema validation rules | M | P0 | M1-056 | [ ] |
| M1-058 | Add business validation rules | M | P1 | M1-057 | [ ] |
| M1-059 | Write validation tests | M | P1 | M1-057 | [ ] |

### Artifacts

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-060 | Create `IArtifactService` interface | S | P0 | M0-005 | [ ] |
| M1-061 | Implement `BlobArtifactService` | M | P0 | M1-060 | [ ] |
| M1-062 | Store under `artifacts/{taskId}/` | S | P0 | M1-061 | [ ] |
| M1-063 | Enforce artifact size limits | M | P1 | M1-061 | [ ] |
| M1-064 | Implement `GET /api/tasks/{taskId}/artifacts` | M | P1 | M1-061 | [ ] |
| M1-065 | Store `snapshot.txt` | S | P0 | M1-061 | [ ] |
| M1-066 | Store `page.meta.json` | S | P0 | M1-061 | [ ] |
| M1-067 | Store `recipe.jsonld` | S | P1 | M1-061 | [ ] |
| M1-068 | Store `draft.recipe.json` | S | P0 | M1-061 | [ ] |
| M1-069 | Write artifact storage tests | M | P1 | M1-061 | [ ] |

### Blazor UI

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-070 | Add "Recipe Ingest Agent" to navigation | XS | P0 | - | [ ] |
| M1-071 | Create `IngestWizard.razor` with URL input | M | P0 | M1-070 | [ ] |
| M1-072 | Implement task creation call | S | P0 | M1-071, M1-004 | [ ] |
| M1-073 | Create `TaskProgress.razor` component | M | P0 | M1-012 | [ ] |
| M1-074 | Connect progress to SignalR | M | P0 | M1-073 | [ ] |
| M1-075 | Create `ReviewReady.razor` component | L | P0 | - | [ ] |
| M1-076 | Display recipe fields | M | P0 | M1-075 | [ ] |
| M1-077 | Display validation warnings/errors | M | P0 | M1-075 | [ ] |
| M1-078 | Display provenance | S | P0 | M1-075 | [ ] |
| M1-079 | Add artifact links | S | P1 | M1-075, M1-064 | [ ] |
| M1-080 | Implement wizard navigation | M | P0 | M1-071, M1-073, M1-075 | [ ] |

### E2E Tests

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M1-081 | E2E test: URL with JSON-LD ? ReviewReady | L | P0 | M1-009 | [ ] |
| M1-082 | E2E test: URL without JSON-LD ? LLM ? ReviewReady | L | P0 | M1-081 | [ ] |
| M1-083 | E2E test: Fetch failure ? Failed state | M | P0 | M1-013 | [ ] |

---

## Milestone 2 — Commit + Lifecycle

### Commit Endpoint

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M2-001 | Create `ImportRecipeRequest` model | S | P0 | M0-006 | [ ] |
| M2-002 | Create `ImportRecipeResponse` model | S | P0 | - | [ ] |
| M2-003 | Implement `POST /api/recipes/import` skeleton | M | P0 | M2-001 | [ ] |
| M2-004 | Add task state validation (must be ReviewReady) | S | P0 | M2-003 | [ ] |
| M2-005 | Add expiration check ? 410 | S | P0 | M2-003 | [ ] |
| M2-006 | Implement commit idempotency | M | P0 | M2-003 | [ ] |
| M2-007 | Implement optimistic concurrency (ETag) | M | P0 | M2-003 | [ ] |
| M2-008 | Return 409 on ETag mismatch | S | P0 | M2-007 | [ ] |
| M2-009 | Copy RecipeDraft.Source to Recipe.Source | S | P0 | M2-003 | [ ] |
| M2-010 | Compute UrlHash if not present | S | P0 | M2-009, M0-011 | [ ] |
| M2-011 | Query for duplicate by UrlHash, add warning | M | P1 | M2-010 | [ ] |
| M2-012 | Assign Recipe.Id, set timestamps | S | P0 | M2-003 | [ ] |
| M2-013 | Persist to Cosmos recipes container | M | P0 | M2-012 | [ ] |
| M2-014 | Update TaskState to Committed | S | P0 | M2-013 | [ ] |
| M2-015 | Test: successful commit | M | P0 | M2-013 | [ ] |
| M2-016 | Test: commit idempotency | M | P0 | M2-006 | [ ] |
| M2-017 | Test: concurrent commits (409) | M | P0 | M2-007 | [ ] |
| M2-018 | Test: commit after expiration (410) | M | P0 | M2-005 | [ ] |

### Reject Endpoint

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M2-019 | Implement `POST /api/tasks/{taskId}/reject` | M | P0 | - | [ ] |
| M2-020 | Validate task is ReviewReady | S | P0 | M2-019 | [ ] |
| M2-021 | Transition to Rejected status | S | P0 | M2-019 | [ ] |
| M2-022 | Return 200 with terminal state | S | P0 | M2-019 | [ ] |
| M2-023 | Block commit for rejected tasks | S | P0 | M2-021, M2-003 | [ ] |
| M2-024 | Test: reject blocks commit | M | P0 | M2-023 | [ ] |

### Expiration Job

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M2-025 | Create `DraftExpirationService` BackgroundService | M | P0 | M0-015 | [ ] |
| M2-026 | Query for stale ReviewReady tasks | M | P0 | M2-025 | [ ] |
| M2-027 | Transition to Expired status | S | P0 | M2-026 | [ ] |
| M2-028 | Configure run interval | S | P1 | M2-025 | [ ] |
| M2-029 | Register in Aspire AppHost | S | P0 | M2-025 | [ ] |
| M2-030 | Test: expiration marks stale tasks | M | P1 | M2-027 | [ ] |

### UI Actions

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M2-031 | Add Commit button to ReviewReady | S | P0 | M1-075 | [ ] |
| M2-032 | Implement Commit action | M | P0 | M2-031, M2-003 | [ ] |
| M2-033 | Show success with recipe link | S | P0 | M2-032 | [ ] |
| M2-034 | Show error messages | S | P0 | M2-032 | [ ] |
| M2-035 | Add Reject button | S | P0 | M1-075 | [ ] |
| M2-036 | Implement Reject action | M | P0 | M2-035, M2-019 | [ ] |
| M2-037 | Add terminal state indicators | M | P1 | M1-075 | [ ] |
| M2-038 | Disable buttons on terminal states | S | P1 | M2-037 | [ ] |

---

## Milestone 3 — Verbatim Guardrails + Repair

### Similarity Detection

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M3-001 | Create `ISimilarityDetector` interface | S | P0 | M0-004 | [ ] |
| M3-002 | Implement tokenization | M | P0 | M3-001 | [ ] |
| M3-003 | Implement contiguous token overlap detection | L | P0 | M3-002 | [ ] |
| M3-004 | Implement 5-gram Jaccard similarity | L | P0 | M3-002 | [ ] |
| M3-005 | Produce SimilarityReport | M | P0 | M3-003, M3-004 | [ ] |
| M3-006 | Test: tokenization | S | P1 | M3-002 | [ ] |
| M3-007 | Test: token overlap | M | P1 | M3-003 | [ ] |
| M3-008 | Test: Jaccard similarity | M | P1 | M3-004 | [ ] |

### Integration into Validate

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M3-009 | Add similarity check in Validate phase | M | P0 | M1-009, M3-001 | [ ] |
| M3-010 | Compare source vs Description/Instructions | M | P0 | M3-009 | [ ] |
| M3-011 | Add to ValidationReport | S | P0 | M3-010 | [ ] |
| M3-012 | Add SimilarityReport to RecipeDraft | S | P0 | M3-010 | [ ] |
| M3-013 | Store similarity.json artifact | S | P0 | M3-010 | [ ] |

### RepairParaphrase

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M3-014 | Create repair paraphrase prompt template | M | P0 | M0-039 | [ ] |
| M3-015 | Implement RepairParaphrase phase | L | P0 | M1-009 | [ ] |
| M3-016 | Trigger if AutoRepair enabled and error threshold exceeded | M | P0 | M3-015, M0-016 | [ ] |
| M3-017 | Prompt LLM to rephrase sections | M | P0 | M3-015 | [ ] |
| M3-018 | Re-run similarity check | M | P0 | M3-017 | [ ] |
| M3-019 | Store repair artifact | S | P0 | M3-017 | [ ] |
| M3-020 | Mark as error if still violating | S | P0 | M3-018 | [ ] |
| M3-021 | Test: high similarity triggers warning | M | P1 | M3-011 | [ ] |
| M3-022 | Test: AutoRepair reduces similarity | M | P1 | M3-018 | [ ] |

### UI

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M3-023 | Add SimilarityReport section to ReviewReady | M | P0 | M1-075 | [ ] |
| M3-024 | Show overlap and similarity values | S | P0 | M3-023 | [ ] |
| M3-025 | Show policy status | S | P0 | M3-023 | [ ] |
| M3-026 | Indicate repair attempt outcome | S | P1 | M3-023 | [ ] |
| M3-027 | Block commit if error and repair failed | M | P0 | M3-020, M2-031 | [ ] |

---

## Milestone 4 — Query Discovery

### Search Abstraction

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M4-001 | Create `SearchRequest` record | S | P0 | - | [ ] |
| M4-002 | Create `SearchCandidate` record | S | P0 | - | [ ] |
| M4-003 | Create `ISearchProvider` interface | S | P0 | M4-001, M4-002 | [ ] |

### Bing Implementation

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M4-004 | Create `BingSearchOptions` | S | P0 | - | [ ] |
| M4-005 | Implement `BingSearchProvider` | L | P0 | M4-003 | [ ] |
| M4-006 | Call Bing API and parse response | M | P0 | M4-005 | [ ] |
| M4-007 | Map to SearchCandidate | S | P0 | M4-006 | [ ] |
| M4-008 | Add quota controls | M | P1 | M4-005 | [ ] |
| M4-009 | Add allow/deny list filtering | M | P1 | M4-007 | [ ] |
| M4-010 | Add rate limiting | S | P1 | M4-005 | [ ] |
| M4-011 | Write Bing search tests | M | P1 | M4-005 | [ ] |

### Discover Phase

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M4-012 | Implement Discover phase | L | P0 | M1-009, M4-003 | [ ] |
| M4-013 | Parse query from payload | S | P0 | M4-012 | [ ] |
| M4-014 | Call search provider | M | P0 | M4-012 | [ ] |
| M4-015 | Store candidates.json artifact | S | P0 | M4-014 | [ ] |
| M4-016 | Auto-select top candidate | S | P0 | M4-014 | [ ] |
| M4-017 | Store selectedCandidateIndex | S | P0 | M4-016 | [ ] |
| M4-018 | Update progress weights | S | P1 | M4-012 | [ ] |

### Gateway

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M4-019 | Extend payload for mode Query | S | P0 | M1-001 | [ ] |
| M4-020 | Route Query mode to Discover | M | P0 | M4-019, M1-004 | [ ] |
| M4-021 | Test: query task creation | M | P1 | M4-020 | [ ] |

### UI

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M4-022 | Add URL/Query mode toggle | S | P0 | M1-071 | [ ] |
| M4-023 | Create query input form | M | P0 | M4-022 | [ ] |
| M4-024 | Create CandidateList component | M | P1 | M4-015 | [ ] |
| M4-025 | Display candidates after discovery | M | P1 | M4-024 | [ ] |
| M4-026 | Add "Try different candidate" action | M | P2 | M4-025 | [ ] |

---

## Milestone 5 — Normalize Pass

### Normalize Phase

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M5-001 | Create normalize prompt template | M | P0 | M0-039 | [ ] |
| M5-002 | Create `NormalizePatchOperation` with riskCategory | S | P0 | - | [ ] |
| M5-003 | Implement Normalize phase | L | P0 | M1-009 | [ ] |
| M5-004 | Support trigger from ReviewReady | M | P0 | M5-003 | [ ] |
| M5-005 | Support mode normalize with recipeId | M | P0 | M5-003 | [ ] |
| M5-006 | Fetch stored recipe from Cosmos | M | P0 | M5-005 | [ ] |
| M5-007 | Prompt LLM for JSON Patch | M | P0 | M5-003 | [ ] |
| M5-008 | Parse and validate patch | M | P0 | M5-007 | [ ] |
| M5-009 | Test-apply patch | M | P0 | M5-008 | [ ] |
| M5-010 | Store normalize.patch.json | S | P0 | M5-008 | [ ] |
| M5-011 | Store normalize.diff.md | M | P1 | M5-008 | [ ] |
| M5-012 | Test: patch validation | M | P1 | M5-008 | [ ] |

### Gateway

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M5-013 | Extend payload for mode normalize | S | P0 | M1-001 | [ ] |
| M5-014 | Handle normalize mode in task creation | M | P0 | M5-013 | [ ] |
| M5-015 | Test: normalize task creation | M | P1 | M5-014 | [ ] |

### Apply Patch

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M5-016 | Create `ApplyPatchRequest` model | S | P0 | M5-002 | [ ] |
| M5-017 | Implement `POST /api/recipes/{id}/apply-patch` | L | P0 | M5-016 | [ ] |
| M5-018 | Validate task and patch | M | P0 | M5-017 | [ ] |
| M5-019 | Apply patch to recipe | M | P0 | M5-017 | [ ] |
| M5-020 | Persist updated recipe | S | P0 | M5-019 | [ ] |
| M5-021 | Update TaskState | S | P0 | M5-020 | [ ] |
| M5-022 | Test: apply patch | M | P0 | M5-019 | [ ] |

### Reject Patch

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M5-023 | Implement reject-patch endpoint | M | P0 | - | [ ] |
| M5-024 | Leave recipe unchanged | S | P0 | M5-023 | [ ] |
| M5-025 | Test: reject leaves unchanged | M | P1 | M5-024 | [ ] |

### UI

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| M5-026 | Create NormalizeDiff component | L | P0 | M5-010 | [ ] |
| M5-027 | Display before/after values | M | P0 | M5-026 | [ ] |
| M5-028 | Color-code by risk category | S | P1 | M5-027 | [ ] |
| M5-029 | Add Apply Patch button | M | P0 | M5-026, M5-017 | [ ] |
| M5-030 | Add Reject Patch button | M | P0 | M5-026, M5-023 | [ ] |
| M5-031 | Add Normalize button to ReviewReady | S | P0 | M1-075, M5-003 | [ ] |
| M5-032 | Add Normalize action to recipe view | S | P1 | M5-005 | [ ] |

---

## Cross-Cutting Tasks

### MCP Server

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| CC-001 | Implement `prompt_list` tool | M | P1 | M0-023 | [ ] |
| CC-002 | Implement `prompt_get` tool | M | P1 | M0-023 | [ ] |
| CC-003 | Implement `prompt_get_active` tool | M | P1 | M0-023 | [ ] |
| CC-004 | Implement `prompt_render` tool | M | P1 | M0-026 | [ ] |

### Observability

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| CC-005 | Add structured logging | M | P1 | M1-009 | [ ] |
| CC-006 | Add tasks created metric | S | P2 | M1-004 | [ ] |
| CC-007 | Add phase duration metric | M | P2 | M1-009 | [ ] |
| CC-008 | Add phase failures metric | S | P2 | M1-013 | [ ] |
| CC-009 | Add extraction method metric | S | P2 | M1-054 | [ ] |
| CC-010 | Add guardrail violations metric | S | P2 | M3-011 | [ ] |
| CC-011 | Add circuit breaker trips metric | S | P2 | M1-026 | [ ] |

### Retention

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| CC-012 | Configure 180-day retention for committed | M | P2 | M1-061 | [ ] |
| CC-013 | Configure 30-day retention for others | M | P2 | M1-061 | [ ] |

### Golden Set

| ID | Task | Size | Priority | Deps | Status |
|----|------|------|----------|------|--------|
| CC-014 | Curate 10-15 recipe fixtures | L | P2 | M1-081 | [ ] |
| CC-015 | Create golden set test suite | L | P2 | CC-014 | [ ] |
| CC-016 | Add to CI pipeline | M | P2 | CC-015 | [ ] |

---

## Summary

| Milestone | Tasks | P0 | P1 | P2 |
|-----------|-------|----|----|-----|
| M0 | 43 | 31 | 11 | 1 |
| M1 | 83 | 57 | 25 | 1 |
| M2 | 38 | 29 | 9 | 0 |
| M3 | 27 | 18 | 8 | 1 |
| M4 | 26 | 14 | 10 | 2 |
| M5 | 32 | 22 | 9 | 1 |
| CC | 16 | 0 | 8 | 8 |
| **Total** | **265** | **171** | **80** | **14** |

---

## Critical Path

```
M0-001 ? M0-006 ? M1-001 ? M1-004 ? M1-008 ? M1-009 ? M1-081
                                         ?
M0-020 ? M0-023 ? M0-026 ? M1-046 ? M1-045 ? M1-053
                                         ?
M1-015 ? M1-017 ? M1-025 ? M1-026 (Fetch + Circuit Breaker)
              ?
M1-031 ? M1-033 ? M1-034 ? M1-040 (Sanitization + JSON-LD)
              ?
M1-060 ? M1-061 ? M1-065...M1-068 (Artifacts)
              ?
M2-003 ? M2-013 ? M2-014 (Commit)
              ?
M3-001 ? M3-005 ? M3-009 ? M3-015 (Guardrails)
              ?
M4-003 ? M4-005 ? M4-012 (Discovery)
              ?
M5-003 ? M5-017 ? M5-019 (Normalize)