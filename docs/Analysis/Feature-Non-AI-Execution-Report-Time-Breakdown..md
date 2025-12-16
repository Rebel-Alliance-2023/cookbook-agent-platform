## Recipe Ingest Agent — January 2024 Work Plan (no AI coding tools)


---

## Assumptions (Jan 2024)

* Start: **Tue Jan 2, 2024**. End: **Wed Jan 31, 2024**.
* Capacity: **5 engineers (Developer-01..05)**, equal ability, **8h/day**, ~**21 workdays** in the month.
* Scope target for January: **Milestone 0 + Milestone 1 + Milestone 2** as the “production-credible vertical slice” (URL import → ReviewReady → Commit/Reject/Expire). This matches the Implementation Plan’s “vertical slice first” guidance.  
* Stretch (only if ahead): start Milestone 3 guardrails. 
* UI requirements include Import Wizard + Prompt Selector + Diff Viewer (Diff Viewer is Milestone 5; not planned for January unless “stretch”). 

---

## Workstreams (how the plan is partitioned)

Implementation Plan workstreams imply parallel execution across **Shared models**, **Gateway**, **Orchestrator**, **Blazor UI**, and (supporting) **Infrastructure/Testing**. 

* **Developer-01**: Shared domain models + URL utils + guardrails groundwork
* **Developer-02**: Gateway endpoints (tasks, prompts, commit/reject), contracts, validation
* **Developer-03**: Orchestrator pipeline (phase runner, fetch/sanitize, extract/validate), background expiration service
* **Developer-04**: Blazor UI (import wizard, progress, review, terminal states)
* **Developer-05**: Test harness + integration tests + artifact/observability glue (and fills gaps)

---

## Schedule overview (calendar)

**Week 1 (Jan 2–5):** Milestone 0 foundation (types/options/prompt registry)
**Week 2 (Jan 8–12):** Milestone 1 core pipeline + basic UI scaffold
**Week 3 (Jan 15–19):** Milestone 1 hardening (SSRF/circuit breaker/JSON-LD/repair loops/artifacts) + UI review
**Week 4 (Jan 22–26):** Milestone 2 commit/reject/expire + UI actions + end-to-end tests
**Week 5 (Jan 29–31):** Buffer + bug-fix + (stretch) start guardrails (Milestone 3)

---

## Detailed work plan (tasks, owners, durations)

> Durations are **engineering days** (8h) and assume normal pre-AI dev/test cadence with code review.

### Week 1 — Milestone 0: Foundation (Models/Options/Prompt Registry)

Milestone 0 deliverables: models + URL hash utilities + options + Prompt Registry + agent type validation. 

| ID    | Task                                                                                             |  Owner | Duration | Depends on   |
| ----- | ------------------------------------------------------------------------------------------------ | -----: | -------: | ------------ |
| M0-01 | Add `RecipeSource`, add optional `Recipe.Source`, add `RecipeDraft` wrapper + supporting records | Dev-01 |     2.0d | —            |
| M0-02 | `UrlNormalizer` + `UrlHasher` (base64url SHA256, trimmed) + unit tests                           | Dev-01 |     1.5d | —            |
| M0-03 | `IngestOptions` + `IngestGuardrailOptions` + config binding + unit tests                         | Dev-05 |     1.5d | —            |
| M0-04 | Prompt Registry: Cosmos container `prompts` + `PromptTemplate` model                             | Dev-02 |     1.5d | —            |
| M0-05 | `IPromptRenderer` + `ScribanPromptRenderer` + truncation rules + `PromptRenderException`         | Dev-02 |     2.0d | M0-04        |
| M0-06 | Gateway Prompt CRUD endpoints + seed `ingest.extract.v1`                                         | Dev-02 |     2.0d | M0-04, M0-05 |
| M0-07 | AgentType validation middleware (`KnownAgentTypes`: Ingest/Research/Analysis) + tests            | Dev-03 |     0.5d | —            |
| M0-08 | Serialization tests for new models (Cosmos compatibility)                                        | Dev-05 |     1.0d | M0-01        |
| M0-09 | Integration smoke test: prompt CRUD against Cosmos emulator                                      | Dev-05 |     1.0d | M0-06        |

**Week 1 outputs:** foundation is in place to support URL import phases and prompt selection (per spec/plan). 

---

### Week 2–3 — Milestone 1: URL Import Vertical Slice (Fetch → Extract → Validate → ReviewReady)

Milestone 1 phase pipeline and UI review requirements.  

| ID    | Task                                                                                                                |  Owner | Duration | Depends on          |
| ----- | ------------------------------------------------------------------------------------------------------------------- | -----: | -------: | ------------------- |
| M1-01 | Gateway: `POST /api/tasks` ingest contract (mode Url), ThreadId generation, validation, tests                       | Dev-02 |     2.5d | M0-07               |
| M1-02 | Orchestrator: `IngestPhaseRunner` pipeline + TaskState progress/eventing                                            | Dev-03 |     2.5d | M1-01               |
| M1-03 | Fetch service: scheme allowlist, size limits, retries/backoff, per-domain rate limiting                             | Dev-03 |     2.0d | M1-02               |
| M1-04 | SSRF protections (block private IPs + DNS verification)                                                             | Dev-03 |     2.0d | M1-03               |
| M1-05 | Circuit breaker (domain failure tracking/block window)                                                              | Dev-03 |     1.5d | M1-03               |
| M1-06 | Sanitization pipeline: generate “snapshot.txt” within content budget                                                | Dev-05 |     2.0d | M1-03               |
| M1-07 | JSON-LD detection + Schema.org Recipe extraction                                                                    | Dev-01 |     2.0d | M1-03               |
| M1-08 | Prompt rendering integration for Extract (url/content/schema variables)                                             | Dev-01 |     1.0d | M0-05, M1-07        |
| M1-09 | LLM extraction path (strict JSON) + `RepairJson` loop + artifacts                                                   | Dev-03 |     2.5d | M1-08               |
| M1-10 | Validate phase: schema/business validation → `ValidationReport`                                                     | Dev-01 |     1.5d | M1-09               |
| M1-11 | Artifact writer to Blob (`snapshot.txt`, `page.meta.json`, `recipe.jsonld`, `draft.recipe.json`) + size enforcement | Dev-05 |     2.5d | M1-06, M1-07, M1-09 |
| M1-12 | Blazor UI: “Recipe Ingest Agent” entry + URL input + start task + streaming progress                                | Dev-04 |     3.0d | M1-01, M1-02        |
| M1-13 | Blazor UI: ReviewReady view (draft fields, validation, provenance, artifact links)                                  | Dev-04 |     3.0d | M1-11               |
| M1-14 | End-to-end tests: “known good JSON-LD URL reaches ReviewReady” + “no JSON-LD uses LLM extraction”                   | Dev-05 |     2.0d | M1-11, M1-13        |
| M1-15 | Hardening pass (timeouts, error codes, Failed transitions, logs)                                                    | Dev-03 |     1.5d | M1-14               |

**Why this matches the spec:** URL import skips discovery and runs fetch/extract/validate, then presents ReviewReady for human action. 

---

### Week 4 — Milestone 2: Commit + Lifecycle (ReviewReady → Commit / Reject / Expire)

Commit/reject/expire behavior and constraints are explicitly required (idempotency, concurrency, expiration).  

| ID    | Task                                                                                               |  Owner | Duration | Depends on   |
| ----- | -------------------------------------------------------------------------------------------------- | -----: | -------: | ------------ |
| M2-01 | Gateway: `POST /api/recipes/import` commit endpoint (state checks + expiration 410)                | Dev-02 |     2.0d | M1-14        |
| M2-02 | Commit idempotency + optimistic concurrency (ETag) + duplicate UrlHash warning                     | Dev-02 |     2.0d | M2-01        |
| M2-03 | Persist canonical `Recipe` into Cosmos (`Recipe.Source` copied from draft source)                  | Dev-02 |     1.0d | M2-02        |
| M2-04 | `POST /api/tasks/{taskId}/reject` endpoint + terminal state rules                                  | Dev-02 |     1.0d | M2-01        |
| M2-05 | Orchestrator: expiration background service (scan ReviewReady → Expired) + tests                   | Dev-03 |     2.0d | M2-01        |
| M2-06 | Blazor UI: Commit/Reject actions + terminal state indicators + disable actions on terminal         | Dev-04 |     2.0d | M2-03, M2-04 |
| M2-07 | Integration tests: commit success, idempotency, concurrent commit 409, commit after expiration 410 | Dev-05 |     2.0d | M2-03, M2-05 |
| M2-08 | Bug bash + polish (error surfacing, state transitions, UX)                                         |    All |     1.0d | M2-07        |

---

### Week 5 — Buffer + (Stretch) start Milestone 3 Guardrails

Guardrails and RepairParaphrase are planned in Milestone 3. 

| ID              | Task                                                                       |  Owner | Duration | Depends on |
| --------------- | -------------------------------------------------------------------------- | -----: | -------: | ---------- |
| BUF-01          | Stabilization buffer (triage + fix + doc)                                  |    All |     2.0d | —          |
| S3-01 (stretch) | Similarity detection service (token overlap + 5-gram Jaccard) + unit tests | Dev-01 |     2.0d | BUF-01     |
| S3-02 (stretch) | Integrate similarity check into Validate phase + store `similarity.json`   | Dev-03 |     1.0d | S3-01      |

---

## Effort summary (January)

* Planned scope (M0–M2): **~45–55 engineer-days** (comfortably fits into Jan capacity of ~105 engineer-days).
* Expected delivery by **Jan 26, 2024** for Milestone 2 “Done”, leaving **Jan 29–31** as buffer/stabilization.

---

## Notes on implementation metrics (what to capture during January)

Since observability/metrics are part of the Implementation Plan’s operational readiness, include instrumentation for: task counts, per-phase duration histograms, failure counts, extraction-method counts, guardrail violation counts, repair attempts, and circuit breaker trips. 
This enables measuring “Implementing a Feature in an existing codebase” using:

* **Lead time**: first commit → ReviewReady → Commit success
* **Cycle time per milestone**: M0, M1, M2
* **Defect rate**: failures per phase + rework count
* **Integration friction**: number of cross-service issues found in Week 4

---

## Developer assignment recap

* **Dev-01:** Shared domain + URL hashing + validation + (stretch) guardrails
* **Dev-02:** Gateway endpoints (tasks, prompt CRUD, commit/reject) + contracts/tests
* **Dev-03:** Orchestrator pipeline (fetch/extract/validate/reviewready) + expiration background job
* **Dev-04:** Blazor UI wizard/progress/review/commit/reject/terminal UX (per UI requirements) 
* **Dev-05:** Integration tests, artifact plumbing, test fixtures, CI-style “golden set” groundwork (optional but recommended) 

---
## Appendix: Salary Cost Calculations (Assuming **$150,000/year** per engineer)

### A. Per-engineer salary cost

* **Annual:** $150,000
* **Monthly (annual / 12):** $150,000 / 12 = **$12,500**
* **Workday rate (assuming 260 workdays/year):** $150,000 / 260 = **$576.923/day**
* **January 2024 salary cost (21 workdays, workday-rate method):**
  $576.923 × 21 = **$12,115.38**
* **January 2024 salary cost (monthly method):** **$12,500**

### B. Team salary cost (5 engineers)

* **Annual:** $150,000 × 5 = **$750,000**
* **January 2024 (monthly method):** $12,500 × 5 = **$62,500**
* **January 2024 (21 workdays @ workday-rate):** $12,115.38 × 5 = **$60,576.90**

### C. Total cost to implement the feature (from plan effort: **~45–55 engineer-days**)

Using the workday rate **$576.923/day**:

* **Low estimate (45 engineer-days):** 45 × $576.923 = **$25,961.54**
* **High estimate (55 engineer-days):** 55 × $576.923 = **$31,730.77**

### D. Total cost to do the work (final)

**Estimated salary cost to implement the feature (planned effort basis): *$25,961.54 – $31,730.77*.**
