# From Engineer-Days to Minutes: A Feature Delivery Case Study (Recipe Ingest Agent)

## A Practical Model for Estimating Feature Work (With Real Execution Data)

> **Status:** Draft

---

## Abstract

This case study compares **traditional feature delivery (non‑AI)** against **AI-assisted automation** when implementing the same feature from a well-articulated specification and implementation plan.

Using the Recipe Ingest Agent as the workload, the analysis contrasts a conventional milestone/workstream estimate expressed in **engineer-days** with recorded execution time captured from real task execution reports (manual early, automated later). The result is a pragmatic model for estimating feature work—and a measured view of where the time actually goes.

---

## The lead story: Non‑AI estimate vs AI‑assisted execution

The purpose of this article is to quantify the time delta between:
- a **non‑AI planning + implementation model** (engineer-days derived from the milestone plan), and
- an **AI-automated execution model** (minutes/hours measured from task execution reports).

### Headline result (this case)

- **Non‑AI estimate (planned effort):** ~**45–55 engineer-days**
- **AI‑assisted recorded execution time:** **6h 37m** (397 minutes) by a single engineer

Interpreting the recorded time as engineer-days (8h/day): **0.83 engineer-days**.

This yields an apparent speedup of roughly **54×–67×** for this specific workload and measurement method.

> Important: this comparison is intentionally “engineer-days vs minutes” rather than a controlled experiment. The non‑AI number is an estimate derived from the plan; the AI number is measured execution time, and the organizational context (team vs single engineer) differs. The remainder of the article focuses on what this comparison is useful for (and what it is not).

---

## Why this case study

Most software delivery discussions compare AI vs non‑AI using synthetic tasks or self‑reported surveys. This article instead anchors the comparison in a concrete feature with:
- a written **feature specification**
- an explicit **implementation plan** (milestones, workstreams, tasks)
- **execution reports** with recorded durations (manual early, automated later)

The primary goal is to compare **non‑AI feature delivery (engineer-day estimation)** to **AI-assisted delivery (measured minutes/hours)**. Secondary goals include identifying which work categories are accelerated, which remain stubbornly “glue-heavy,” and how tracking method affects conclusions.

---

## Context

### Project background: Cookbook Agent Platform

The Recipe Ingest Agent is implemented inside an existing .NET-based “Cookbook Agent Platform” that is designed for long-running, user-visible workflows. The platform is orchestrated with .NET Aspire and exposes an interactive Blazor Server UI that streams progress updates for multi-phase operations.

At a high level, the platform uses a consistent pipeline pattern:
- A **Gateway** service receives requests and persists/validates task metadata.
- An **Orchestrator** service executes the phase pipeline and reports progress.
- **Redis Streams / SignalR** provide work-queuing and real-time UX updates.
- **Cosmos DB** stores durable domain documents and task/session state.
- **Blob Storage** stores large artifacts (snapshots, diffs, exports), with metadata tracked in Cosmos.

This case study focuses on adding a new feature to that established pattern rather than building a greenfield system.

### Feature summary: Recipe Ingest Agent

The Recipe Ingest Agent adds a new **Ingest** capability that turns unstructured web content into a canonical recipe document through an explicit, observable workflow.

Core behavior:
- **Inputs:** a single URL (primary) and, optionally, a query-based discovery mode.
- **Phases:** Discover (optional) → Fetch → Extract → Validate → ReviewReady.
- **Human-in-the-loop:** once the draft reaches **ReviewReady**, the UI presents artifacts and validation results, and a user explicitly **Commits** or **Rejects**.
- **Durability:** large artifacts (snapshots, JSON-LD, draft JSON) are written to Blob Storage; canonical recipes are persisted to Cosmos.
- **Operational rules:** idempotent commit, optimistic concurrency, expiration of stale tasks, and guardrails/repair loops as later enhancements.

This is a representative “existing-codebase feature” because it touches multiple boundaries at once: contracts, orchestration, storage, UI, and end-to-end test harnesses.

### Milestone model used

- Milestone 0: foundation (models/options/prompt registry)
- Milestone 1: URL import vertical slice (fetch → extract → validate → review-ready)
- Milestone 2: commit/reject/expire
- (Later milestones: guardrails, diff viewer, etc.)

---

## The estimating model

### 1) Decompose by workstream

Use workstreams that map to real code boundaries:
- shared models + utilities
- API surface (gateway)
- orchestration/pipeline
- UI
- tests + infra glue

### 2) Estimate in “engineer-days” per task

- Use 1–3 day tasks when possible.
- Include explicit tasks for:
  - validation and hardening
  - integration tests
  - operational behaviors (expiration/idempotency)

### 3) Convert estimate to cost

- Workday rate = annual salary / 260
- Cost = engineer-days × workday rate

---

## What we actually measured

### Why the execution reports are asymmetrical

After producing the first two execution reports, it became clear that GitHub Copilot running inside Visual Studio 2026 could not reliably access wall-clock time from the local machine during tool-driven work (for example, when executing git or .NET CLI commands). In practice, this meant the workflow could produce correct code changes, but the reporting layer could not consistently capture trustworthy start/end timestamps.

To address this, a dedicated MCP server named **`time-tracker`** was created so Copilot could explicitly query current time and record task/session timestamps during execution. Later reports therefore have higher-fidelity, automated timing at task granularity, while earlier reports remain session-level and hand-tracked.

### Execution data sources

- Early execution reports: manual time tracking (session totals)
- Later execution reports: automated time tracking (task-level)

### Normalizing mixed tracking

- Prefer **session totals** when available for “actual time spent.”
- Use task-level sums when session totals are absent.
- Call out bundled work explicitly when tasks were “included” as part of another task.

---

## Findings

## 1) The time is not evenly distributed

**Actual execution data shows a classic long-tail distribution: the initial vertical slice accounts for ~half of recorded effort, with several smaller but still meaningful spikes later.** 

### Breakdown by milestone (recorded)

| Milestone / workstream                                                    | Recorded time | Share of recorded time |
| ------------------------------------------------------------------------- | ------------: | ---------------------: |
| **M1 — URL Import vertical slice**                                        |    **3h 25m** |             **51.6%**  |
| M2 — Commit & lifecycle wiring                                            |           11m |                  2.8%  |
| M3 — Similarity + repair/paraphrase + UI touchups                         |           31m |                  7.8%  |
| **M4 — Query discovery (dual search providers + UI + E2E)**               |    **1h 01m** |             **15.4%**  |
| **M5 — Normalize mode (JSON Patch + diff artifacts + pipeline)**          |       **56m** |             **14.1%**  |
| CC — Cross-cutting (MCP tools + observability + retention + golden tests) |           33m |                  8.3%  |

### Within Milestone 1, time clustered around integration work (session-level)

| M1 focus area                                                         | Recorded time |
| --------------------------------------------------------------------- | ------------: |
| Gateway task contract + orchestrator phase runner                     |          50m  |
| Fetch + security protections + sanitization + extraction + validation |          90m  |
| Artifact storage + Blazor UI + end-to-end tests                       |          65m  |

*Takeaway: “core logic” was only one slice; integration, safety, artifacts, and tests were a large—and predictable—portion of the actual work.*

---

### 2) Instrumentation changes the quality of the data

- Manual tracking: coarse but still useful for order-of-magnitude.
- Automated tracking: exposes true distribution and overhead.

## 3) Estimation accuracy improves when “glue work” is first-class

Treating “glue work” as explicit deliverables (with explicit tasks) improves estimate quality, because much of feature delivery time lands in the seams between components (the “glue code” that makes parts interoperate). ([Wikipedia][1])

Examples from this case study where glue work dominated or surprised:

* **Configuration + options binding + secret management**

  * Search-provider enablement required wiring that’s easy to undercount: defaults, validation, and developer-safe setup (e.g., user-secrets workflows for keys/cx). 

* **Error pathways + fallback behavior**

  * Resilience required more than happy-path code: explicit fallback policy, transient/rate-limit handling, and recording fallback metadata into pipeline context. 

* **Artifacts and human-review UX**

  * Normalize isn’t just “apply patch”: it required storing patch artifacts and generating a human-readable diff (`normalize.patch.json`, `normalize.diff.md`). 

* **Cross-service contracts and phase orchestration**

  * Status enums, progress weighting, context shape, and “phase runner” plumbing are integration surfaces that need explicit estimation. 

* **Test scaffolding and end-to-end validation**

  * Test volume became a first-class workstream (unit + E2E), including major E2E expansion for query discovery. 

*Practical rule: if something has to be configured, observed, reviewed, retried, or tested end-to-end, it should be estimated as a named workstream—not hidden inside “implementation.”*

---

---

## Practical template: how to estimate the next feature

1. Start with a vertical-slice milestone plan.
2. Partition by workstream.
3. Make hardening/idempotency/expiration explicit.
4. Require an end-to-end test milestone.
5. Capture actuals in the same shape as the plan.

---

## Limitations

- Single feature.
- Single engineer execution for the measured run(s).
- Mixed tracking methods.

---

## Conclusion

* **Estimate by workstreams, not only by “domain logic.”** Make contracts, orchestration, configuration, artifacts, UI, and tests explicit slices of the plan.
* **Expect the first vertical slice to dominate.** Once the pipeline exists, later milestones become smaller spikes (but still meaningful), especially for cross-cutting concerns.
* **Make glue work measurable.** Track configuration work, error handling, fallbacks, and observability as named tasks so estimates converge over time.
* **AI changes the unit of progress.** When implementation compresses into minutes, verification, integration, and test quality become the limiting factors.
* **Instrumentation is part of delivery.** Reliable timestamps and task-level telemetry are required to compare approaches and improve estimation accuracy.

---

## Appendix

- Links / references to:
  - Feature specification
  - Implementation plan
  - Non-AI execution breakdown
  - AI execution breakdown


[1]: https://en.wikipedia.org/wiki/Glue_code?utm_source=chatgpt.com "Glue code"