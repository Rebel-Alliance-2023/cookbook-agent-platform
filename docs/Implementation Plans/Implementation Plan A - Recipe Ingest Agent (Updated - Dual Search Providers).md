# Problem

The current Implementation Plan A supports query-based recipe discovery via a single `ISearchProvider` in Milestone 4. This creates early lock-in and makes it harder to validate search quality, limits, and operational behavior across providers—especially in developer mode where low cost, quick iteration, and easy fallback matter most.

# Solution

Implement **two** `ISearchProvider` implementations—**Brave Search API** and **Google Custom Search JSON API**—and add a **Search Provider Selector** in the UI (plus a provider registry endpoint). This establishes a reusable “selector” foundation pattern for future selectable capabilities (e.g., prompt version, LLM provider routing overrides, normalization mode).

---

# Implementation Plan A (Updated): Recipe Ingest Agent

**Target stack:** .NET 10, Aspire-orchestrated microservices, Cosmos DB + Blob Storage, Redis Streams + SignalR  
**Scope of this update:** Milestone 4 (Discovery) + shared selector foundation

---

## 1. Goals (unchanged)

1. Import a recipe from a public web URL into the platform as a reviewable draft (`RecipeDraft`).
2. Enforce human-in-the-loop approval before creating a persisted canonical `Recipe` in Cosmos DB.
3. Preserve provenance and enforce “no large verbatim blocks” paraphrasing policy.
4. Support optional normalization as a patch/diff without blocking initial ingestion.

---

## 2. Dev-mode assumptions (for provider choice)

- App runs locally on a laptop in developer mode.
- Expected discovery usage: **< 100 searches/day**.
- Goal is **low/no cost**, fast iteration, and portability.

---

## 3. Changes Summary

### 3.1 What changes
- Milestone 4 now implements **two discovery providers**:
  - `BraveSearchProvider : ISearchProvider`
  - `GoogleCustomSearchProvider : ISearchProvider`
- Add a **Search Provider Selector** in the UI and a **provider registry endpoint** in the Gateway:
  - `GET /api/ingest/providers/search` returns enabled providers + default
  - Task payload carries selected `providerId` for query imports

### 3.2 What stays the same
- URL import path remains the primary v1 vertical slice.
- `ISearchProvider` remains the stable abstraction for `Ingest.Discover`.
- Discovery remains non-LLM by default; LLM browsing remains optional/flagged.

---

## 4. Updated Deliverables by Milestone

### Milestone 0 — Foundation (unchanged)
No change to scope.

### Milestone 1 — URL Import Vertical Slice (unchanged)
No change to scope.

### Milestone 2 — Commit + Lifecycle (unchanged)
No change to scope.

### Milestone 3 — Verbatim Guardrails + RepairParaphrase (unchanged)
No change to scope.

---

## 5. Milestone 4 (UPDATED) — Query Discovery via Dual ISearchProvider + Selector

### 5.1 Objective
Enable query-driven imports using a deterministic search provider selected per task (Brave or Google), while keeping discovery consistent with the existing phase pipeline.

### 5.2 Deliverables

#### A) Implement two ISearchProviders

**1) BraveSearchProvider**
- Implements `ISearchProvider.SearchAsync(...)` using Brave Search API.
- Supports:
  - API key via local user-secrets
  - configurable endpoint, market/locale, safe-search, result count
  - rate limiting (per provider and per domain downstream)
- Produces normalized `SearchCandidate` items:
  - `Url`, `Title`, `Snippet`, `SiteName`, `Score`

**2) GoogleCustomSearchProvider**
- Implements `ISearchProvider.SearchAsync(...)` using Google Custom Search JSON API.
- Requires:
  - API key + Programmable Search Engine ID (cx)
- Supports:
  - query parameters, result count, language, country
  - site-restricted mode (optional) for recipe-domain allowlists
- Produces the same normalized `SearchCandidate` items.

**Debug artifacts**
- Store two artifacts per query:
  - `candidates.normalized.json`
  - `candidates.raw.json` (provider’s raw response; for debugging only)

#### B) Implement Search Provider Resolver

Add a small resolver to select a provider by id:

```csharp
public interface ISearchProviderResolver
{
    ISearchProvider Resolve(string providerId);
    IReadOnlyList<SearchProviderDescriptor> ListEnabled();
}

public record SearchProviderDescriptor(
    string Id,
    string DisplayName,
    bool Enabled,
    bool IsDefault,
    Dictionary<string,string?> Capabilities);
```

- `Resolve(providerId)` throws a structured error if provider is disabled/unknown.
- `ListEnabled()` is used by the UI selector and by server-side defaulting.

#### C) Add Provider Registry Endpoint (Gateway)

**Endpoint:** `GET /api/ingest/providers/search`

**Response example:**
```json
{
  "defaultProviderId": "brave",
  "providers": [
    {
      "id": "brave",
      "displayName": "Brave Search",
      "enabled": true,
      "capabilities": { "maxRps": "1", "notes": "Free tier suitable for dev" }
    },
    {
      "id": "google-cse",
      "displayName": "Google Custom Search",
      "enabled": true,
      "capabilities": { "notes": "100 queries/day free" }
    }
  ]
}
```

#### D) Update Task Payload Contract (Query mode)

Add a search selection block:

```json
"payload": {
  "mode": "Query",
  "query": "tonkotsu ramen",
  "search": { "providerId": "brave" }
}
```

Server behavior:
- If `search.providerId` omitted, Gateway uses `defaultProviderId`.
- Unknown/disabled provider → `400 INVALID_SEARCH_PROVIDER`.

#### E) UI: Search Provider Selector (foundation pattern)

Add a selector UI control to the Query import wizard:
- Populates from `GET /api/ingest/providers/search`
- Stores selected provider per task (query import)
- Default selection uses `defaultProviderId`

**Foundation pattern:** implement as a reusable “selector” component that can later support:
- prompt version selection
- normalization mode selection
- extraction policy selection

### 5.3 Retry / Failure / Fallback Policy

Default behavior (recommended):
- If the selected provider fails (quota, 429, auth, transient), fail `Ingest.Discover` with a structured error.
- Optional operator flag `Ingest:Search:AllowFallback=true`:
  - If enabled, on certain errors (429/quota/transient) retry once on the default provider.
  - Record fallback in `TaskState.Metadata` and in an artifact (`discover.fallback.json`).

### 5.4 Dev-mode cost posture (informational)

This plan supports staying near $0 in developer mode:
- Google Custom Search JSON API includes a free daily allowance (suitable for < 100 searches/day).
- Brave Search API includes a free monthly allowance (also suitable for dev usage).

(See “References” section for the source docs and pricing pages.)

### 5.5 Acceptance Criteria (Milestone 4)

- Query import works end-to-end:
  - `Ingest.Discover` produces candidates artifacts
  - pipeline proceeds to Fetch/Extract/ReviewReady
- UI displays enabled providers and applies chosen provider to a task
- Provider selection is captured in task metadata for audit
- Unknown provider id returns `400 INVALID_SEARCH_PROVIDER`
- Raw + normalized candidates artifacts are stored for debugging

---

## 6. Configuration & Secrets

### 6.1 appsettings.json (non-secret)
```json
{
  "Ingest": {
    "SearchProviders": [
      { "Id": "brave", "DisplayName": "Brave Search", "Enabled": true, "IsDefault": true },
      { "Id": "google-cse", "DisplayName": "Google Custom Search", "Enabled": true, "IsDefault": false }
    ],
    "Search": {
      "AllowFallback": false
    }
  }
}
```

### 6.2 Local user-secrets (dev)
- Brave:
  - `Ingest:Search:Brave:ApiKey`
- Google:
  - `Ingest:Search:Google:ApiKey`
  - `Ingest:Search:Google:SearchEngineId` (cx)

---

## 7. Future-proofing Notes (why this “selector foundation” matters)

With this pattern in place, future selectors can reuse:
- the “capabilities registry endpoint” approach
- the “provider descriptors” response shape
- the same UI selector component

Expected next selectors:
- **Prompt Template Selector** (per phase)
- **Normalization Strategy Selector** (off/suggest/aggressive)
- Optional per-task **LLM provider routing override** (advanced)

---

# References (for pricing/limits)

Google Custom Search JSON API pricing and limits:
```text
https://developers.google.com/custom-search/v1/overview
```

Google Custom Search Site Restricted JSON API pricing:
```text
https://developers.google.com/custom-search/v1/site_restricted_api
```

Brave Search API pricing (developer dashboard plans):
```text
https://api-dashboard.search.brave.com/app/plans
```

Brave Search API product page (pricing overview):
```text
https://brave.com/search/api/
```
