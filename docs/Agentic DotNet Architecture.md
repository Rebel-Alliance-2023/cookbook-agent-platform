# Agentic Cookbook Architecture Software Specification

## Overview

This specification defines the architecture, components, naming conventions, and initial implementation plan for the “Cookbook Agent Platform” built with .NET Aspire. The platform showcases agentic protocols (A2A for agent collaboration and MCP for deterministic tools), real-time user interaction, semi-durable messaging, and durable persistence. The overarching theme is “Cookbook recipes,” integrating research, analysis, nutrition estimation, and artifact generation.

## Goals

- Demonstrate complementary use of A2A (agents) and MCP (tools) in a unified scenario.
- Provide real-time, interactive UX for long-running workflows.
- Enable semi-durable state with TTL while ensuring durable persistence.
- Support multiple LLM providers (OpenAI and Anthropic) with configurable routing.
- Adopt .NET Aspire for orchestration, observability, configuration, and environment separation.
- Ensure clean abstractions and resilience to swap implementations (e.g., Service Bus later).

## Architecture

### Runtime and orchestration

- .NET Aspire orchestrates services with:
  - AppHost for composition and local Docker resources.
  - ServiceDefaults for OpenTelemetry, health checks, resiliency.
- ASP.NET 10 Blazor Server for AG-UI (client).
- Deployment targets:
  - Local: Docker Compose (Redis, Azurite, Cosmos Emulator or serverless).
  - Cloud: Azure Container Apps (ACA) with Azure Container Registry (ACR).

### Secrets and configuration

- Development: Use Aspire local “secrets” connection string pattern (per David Pine).
- Publish mode: Use Azure Key Vault, accessing with managed identity.
- Configuration sections:
  - MessagingBus: select implementation (RedisSignalR or AzureServiceBus) and settings.
  - Cosmos: Endpoint, DatabaseName, UseEmulator, Key (dev only).
  - Storage: BlobEndpoint, Containers, UseAzurite.
  - Llm: DefaultProvider, keyed provider configs for OpenAI and Anthropic.

### Data plane

- Cosmos DB (Core SQL API):
  - Containers and partition keys:
    - recipes (/id)
    - sessions (/threadId)
    - messages (/threadId) + timestamp indexing
    - tasks (/taskId)
    - artifacts (/taskId)
    - notes (/threadId)
- Blob Storage:
  - Stores large artifacts (PDF/markdown/images).
  - Metadata persisted in Cosmos; access via SAS or managed identity.

### Messaging and real-time interaction

- SignalR + Redis with TTL:
  - SignalR hub (/agentHub) for per-thread groups; streams events to clients.
  - Redis Streams for task ingestion; Redis Keys with TTL for semi-durable state.
- Future swap: Azure Service Bus behind IMessagingBus abstraction.

### Agentic protocols

- MCP server (Cookbook tools):
  - search.recipes(query, diet?, cuisine?)
  - recipe.get(id)
  - recipe.saveNotes(threadId, notes)
  - nutrition.compute(ingredients[])
  - files.put(name, contentBase64) and files.list(prefix)
- A2A agents:
  - ResearchAgent: discovers candidates, retrieves details, saves notes; streams phases.
  - AnalysisAgent: normalizes recipe, estimates macros, generates shopping list and artifact; streams completion.

### LLM routing

- DI registers both OpenAI (ChatGPT) and Anthropic (Claude).
- LLMRouter:
  - Selects provider per phase (e.g., Claude for narrative, GPT-4o for structured tasks).
  - Polly-based retry/backoff and fallback chaining.
- API keys:
  - Dev: local secrets.
  - Cloud: Key Vault.

### Resilience and observability

- Polly:
  - Decorrelated jitter backoff retry policies.
  - Applied to messaging, Cosmos, Blob, MCP calls, and LLM calls.
- OpenTelemetry:
  - Trace and metrics across Gateway, Orchestrator, A2A, MCP, Redis, Cosmos, Blob.
  - Aspire Dashboard locally; Azure Monitor in cloud.

### Security and governance

- Cosmos/Blob: private endpoints, RBAC/managed identity in cloud.
- Redis: Azure Cache for Redis with TLS; Key Vault for secrets in publish mode.
- Aspire config separation ensures dev vs publish credentials; no keys in code.
- Azure Policy enforces tags, diagnostics, region/SKU, private networking, RBAC.

## Naming conventions

### Solution and repository

- Solution: `CookbookAgentPlatform.sln`
- Repository: `cookbook-agent-platform`

### Folders

- `platform/` (Aspire composition, ServiceDefaults)
- `src/` (application projects)
- `docs/` (specs and architecture)
- `tests/` (unit/integration)
- `.github/` (CI/CD)

### Projects

- `platform/AppHost`
- `platform/ServiceDefaults`
- `src/Cookbook.Platform.Shared`
- `src/Cookbook.Platform.Infrastructure`
- `src/Cookbook.Platform.Storage`
- `src/Cookbook.Platform.Gateway`
- `src/Cookbook.Platform.Orchestrator`
- `src/Cookbook.Platform.Client.Blazor`
- `src/Cookbook.Platform.Mcp`
- `src/Cookbook.Platform.A2A.Research`
- `src/Cookbook.Platform.A2A.Analysis`

### Namespaces

- `Cookbook.Platform.Shared`
- `Cookbook.Platform.Infrastructure`
- `Cookbook.Platform.Storage`
- `Cookbook.Platform.Gateway`
- `Cookbook.Platform.Orchestrator`
- `Cookbook.Platform.Client.Blazor`
- `Cookbook.Platform.Mcp`
- `Cookbook.Platform.A2A.Research`
- `Cookbook.Platform.A2A.Analysis`

### Configuration keys

- `MessagingBus`
- `Cosmos`
- `Storage`
- `Llm`

## Key interfaces and classes

### Messaging

- `IMessagingBus` (Shared): abstraction for sending tasks, publishing events, subscribing, cancellation.
- `MessagingBusOptions` (Shared): implementation selection and settings.
- `RedisSignalRMessagingBus` (Infrastructure): Redis Streams + SignalR groups; TTL-backed state.
- `AzureServiceBusMessagingBus` (Infrastructure): durable messaging (future swap).
- `AgentHub` (Infrastructure): SignalR hub for `/agentHub`.

### Storage

- `CosmosOptions` and repositories:
  - `RecipeRepository`, `SessionRepository`, `MessageRepository`, `TaskRepository`, `ArtifactRepository`, `NotesRepository`.
- `BlobStorageOptions`, `IBlobStorage` service; health checks and seeding.

### Orchestrator and LLM

- `OrchestratorService`: coordinates end-to-end pipeline and integrates agents/tools.
- `AgentPipeline` with `ResearchPhase` and `AnalysisPhase`.
- `ILlmRouter` and `LlmRouterOptions`: selects provider/model with retry/fallback.

### MCP server (Cookbook tools)

- `SearchTools`, `RecipeTools`, `NutritionTools`, `FileTools`.
- Exposed via `ModelContextProtocol.AspNetCore` (`MapMcp`, `WithToolsFromAssembly`).

### A2A agents

- `ResearchAgentServer`: long-running research phases; uses MCP tools.
- `AnalysisAgentServer`: normalization and artifact generation; uses MCP tools.
- Supports SSE streaming and task lifecycle.

### Blazor client (AG-UI)

- Pages: `Cookbook.razor`, `Candidates.razor`, `Timeline.razor`, `ShoppingList.razor`.
- Services: `SignalRClientService`, `ApiClientService`.
- Components: `StreamViewer`.

## Application flow

1. User prompt enters via Blazor → Gateway; SignalR group established per `threadId`.
2. Gateway enqueues task via `IMessagingBus.SendTaskAsync`; session/task persisted in Cosmos.
3. Orchestrator runs pipeline:
   - Calls `ResearchAgentServer` → MCP `search.recipes`/`recipe.get` → save notes → stream progress.
   - User approves candidate.
   - Calls `AnalysisAgentServer` → MCP `nutrition.compute` → generate shopping list → store in Blob → persist artifact metadata in Cosmos → stream completion.
4. Redis TTL maintains semi-durable state; Cosmos & Blob are durable sources of truth.
5. Aspire Dashboard shows end-to-end traces; Polly ensures resilience.

## Local development

- Docker Compose via Aspire:
  - Redis, Azurite, Cosmos Emulator (or serverless instance).
- Configuration:
  - `MessagingBus: Implementation = RedisSignalR`
  - `Cosmos: UseEmulator = true`
  - `Storage: UseAzurite = true`
  - `Llm: DefaultProvider` and keys via local secrets.
- Trust Cosmos Emulator cert if using containerized emulator.
- Health checks verify connectivity to Redis, Cosmos, and Azurite.

## Cloud deployment

- ACA services; images via ACR.
- Networking: private endpoints for Cosmos, Blob, Service Bus (if used later).
- Identity: managed identity for Cosmos and Storage; Key Vault for secrets.
- CI/CD (later):
  - Build, push, deploy; policy compliance checks.

## Open items

- Seed dataset for `recipes` container (JSON sample set).
- Default LLM choice in dev (e.g., Claude for research, GPT-4o for analysis).
- Whether to add PlannerAgent as a phase-3 enhancement.

## Appendix: Configuration examples

### Development

```json
{
  "MessagingBus": {
    "Implementation": "RedisSignalR",
    "RedisConnectionString": "localhost:6379,abortConnect=false",
    "RedisStreamPrefix": "stream:",
    "RedisKeyPrefix": "runs:",
    "RedisDb": 0
  },
  "Cosmos": {
    "Endpoint": "https://localhost:8081",
    "DatabaseName": "cookbook-dev",
    "UseEmulator": true,
    "Key": "COSMOS_EMULATOR_KEY"
  },
  "Storage": {
    "UseAzurite": true,
    "BlobEndpoint": "http://127.0.0.1:10000/devstoreaccount1"
  },
  "Llm": {
    "DefaultProvider": "Claude",
    "OpenAI": { "ApiKey": "OPENAI_KEY", "Model": "gpt-4o" },
    "Anthropic": { "ApiKey": "ANTHROPIC_KEY", "Model": "claude-3-5-sonnet" }
  }
}
```

### Production (publish mode)

```json
{
  "MessagingBus": {
    "Implementation": "RedisSignalR"
  },
  "Cosmos": {
    "Endpoint": "https://<your-cosmos-account>.documents.azure.com",
    "DatabaseName": "cookbook",
    "UseEmulator": false
  },
  "Storage": {
    "UseAzurite": false,
    "BlobEndpoint": "https://<your-storage-account>.blob.core.windows.net"
  },
  "Llm": {
    "DefaultProvider": "Claude"
  }
}
```

> Secrets (Cosmos keys, Redis passwords, LLM API keys) are provided via local secrets in dev and Azure Key Vault in publish mode.

## Conclusion

This specification defines the cohesive agentic cookbook architecture with clear naming, boundaries, and configuration. It positions the platform to deliver a compelling demo while remaining production-minded, with resilience, observability, and security baked in—and the flexibility to swap implementations (e.g., Service Bus) as needs evolve.