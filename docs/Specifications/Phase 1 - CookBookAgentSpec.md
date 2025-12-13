# CookBook Agent Platform Specification

## 1. Introduction

This document provides a comprehensive specification for the **CookBook Agent Platform**, an AI-powered distributed system designed to assist users in finding, organizing, and analyzing recipes. The platform leverages natural language processing through multiple LLM providers (Claude, OpenAI, Gemini) and implements a microservices architecture using .NET Aspire for orchestration.

### 1.1 Purpose

The CookBook Agent Platform enables users to:
- Search for recipes using natural language queries
- Receive AI-scored recipe candidates ranked by relevance
- Analyze selected recipes for nutritional information
- Generate shopping lists and downloadable artifacts
- Experience real-time progress updates via SignalR streaming

### 1.2 Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| Frontend | Blazor Server (Interactive) |
| Orchestration | .NET Aspire 9.3 |
| Database | Azure Cosmos DB (Emulator supported) |
| Cache/Messaging | Redis Streams + SignalR |
| Blob Storage | Azure Blob Storage (Emulator supported) |
| LLM Providers | OpenAI (GPT-4o), Anthropic (Claude Sonnet), Google Gemini (2.5 Flash) |

---

## 2. Architecture Overview

### 2.1 High-Level Architecture

```
???????????????????????????????????????????????????????????????????????????
?                           .NET Aspire AppHost                           ?
???????????????????????????????????????????????????????????????????????????
?                                                                         ?
?  ????????????????    ????????????????    ????????????????????????????  ?
?  ?   Blazor     ??????   Gateway    ??????      Orchestrator        ?  ?
?  ?   Client     ?    ?    API       ?    ?                          ?  ?
?  ????????????????    ????????????????    ????????????????????????????  ?
?         ?                   ?                         ?                 ?
?         ?                   ?                         ?                 ?
?         ?            ????????????????    ????????????????????????????  ?
?         ?            ?    Redis     ?    ?     Agent Pipeline       ?  ?
?         ?            ?  (Streams +  ?    ????????????????????????????  ?
?         ?            ?   SignalR)   ?    ?  Research  ?  Analysis   ?  ?
?         ?            ????????????????    ?   Agent    ?   Agent     ?  ?
?         ?                   ?            ?  (Claude)  ?  (OpenAI)   ?  ?
?         ?                   ?            ????????????????????????????  ?
?         ?            ????????????????              ?                   ?
?         ??????????????  Cosmos DB   ????????????????                   ?
?                      ????????????????                                   ?
?                             ?                                           ?
?                      ????????????????    ????????????????????????????  ?
?                      ? Blob Storage ?    ?       MCP Server         ?  ?
?                      ?  (Artifacts) ?    ?   (Recipe/Search/       ?  ?
?                      ????????????????    ?    Nutrition Tools)      ?  ?
?                                          ????????????????????????????  ?
???????????????????????????????????????????????????????????????????????????
```

### 2.2 Project Structure

| Project | Purpose |
|---------|---------|
| `AppHost` | .NET Aspire host that orchestrates all services |
| `ServiceDefaults` | Shared Aspire configuration (telemetry, health checks) |
| `Cookbook.Platform.Client.Blazor` | Blazor Server UI with real-time updates |
| `Cookbook.Platform.Gateway` | REST API gateway with recipe, session, task, artifact endpoints |
| `Cookbook.Platform.Orchestrator` | Task processor and agent pipeline coordinator |
| `Cookbook.Platform.A2A.Research` | Research Agent - recipe discovery and scoring |
| `Cookbook.Platform.A2A.Analysis` | Analysis Agent - nutrition computation and artifact generation |
| `Cookbook.Platform.Mcp` | Model Context Protocol server with recipe tools |
| `Cookbook.Platform.Infrastructure` | LLM router, SignalR hub, Redis messaging |
| `Cookbook.Platform.Shared` | Domain models, interfaces, DTOs |
| `Cookbook.Platform.Storage` | Cosmos DB repositories, blob storage, database initializer |

---

## 3. Domain Models

### 3.1 Recipe

```csharp
public record Recipe
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<Ingredient> Ingredients { get; init; } = [];
    public List<string> Instructions { get; init; } = [];
    public string? Cuisine { get; init; }
    public string? DietType { get; init; }
    public int PrepTimeMinutes { get; init; }
    public int CookTimeMinutes { get; init; }
    public int Servings { get; init; }
    public NutritionInfo? Nutrition { get; init; }
    public List<string> Tags { get; init; } = [];
    public string? ImageUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
```

### 3.2 Ingredient

```csharp
public record Ingredient
{
    public required string Name { get; init; }
    public double Quantity { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }
}
```

### 3.3 NutritionInfo

```csharp
public record NutritionInfo
{
    public double Calories { get; init; }
    public double ProteinGrams { get; init; }
    public double CarbsGrams { get; init; }
    public double FatGrams { get; init; }
    public double FiberGrams { get; init; }
    public double SodiumMg { get; init; }
    public double SugarGrams { get; init; }
}
```

### 3.4 Session

```csharp
public record Session
{
    public required string Id { get; init; }
    public required string ThreadId { get; init; }
    public string? UserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public SessionStatus Status { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

public enum SessionStatus { Active, Completed, Abandoned }
```

### 3.5 AgentTask

```csharp
public record AgentTask
{
    public required string TaskId { get; init; }
    public required string ThreadId { get; init; }
    public required string AgentType { get; init; }  // "Research" | "Analysis"
    public required string Payload { get; init; }     // JSON payload
    public DateTime CreatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
```

### 3.6 TaskState

```csharp
public record TaskState
{
    public required string TaskId { get; init; }
    public required TaskStatus Status { get; init; }
    public string? CurrentPhase { get; init; }
    public int Progress { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
    public DateTime LastUpdated { get; init; }
}

public enum TaskStatus { Pending, Running, Completed, Failed, Cancelled }
```

---

## 4. Agent Pipeline

### 4.1 Research Phase

**Purpose:** Discover and score recipe candidates based on user query.

**LLM Provider:** Claude (Anthropic)

**Flow:**
1. Parse user query from task payload
2. Search Cosmos DB for matching recipes (by name, description)
3. For each candidate, call LLM to score relevance (0.0 - 1.0)
4. Generate research notes summarizing findings
5. Save notes to Cosmos DB
6. Return ranked `RecipeCandidate` list

**Output:**
```csharp
public record ResearchResult
{
    public List<RecipeCandidate> Candidates { get; init; }
    public string Notes { get; init; }
}

public record RecipeCandidate
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public string? Cuisine { get; init; }
    public string? DietType { get; init; }
    public double RelevanceScore { get; init; }
}
```

### 4.2 Analysis Phase

**Purpose:** Deep analysis of a selected recipe with nutrition computation and artifact generation.

**LLM Provider:** OpenAI (GPT-4o)

**Flow:**
1. Load full recipe from Cosmos DB by ID
2. Compute nutrition estimates per ingredient
3. Generate shopping list using LLM (categorized by: Produce, Meat, Dairy, Pantry, Spices)
4. Generate Markdown recipe card
5. Store artifacts in Azure Blob Storage
6. Return analysis result with artifact URIs

**Output:**
```csharp
public record AnalysisResult
{
    public string RecipeId { get; init; }
    public NutritionSummary Nutrition { get; init; }
    public List<ShoppingListItem> ShoppingList { get; init; }
    public List<string> ArtifactUris { get; init; }
}

public record NutritionSummary
{
    public double CaloriesPerServing { get; init; }
    public double ProteinPerServing { get; init; }
    public double CarbsPerServing { get; init; }
    public double FatPerServing { get; init; }
    public int Servings { get; init; }
}

public record ShoppingListItem
{
    public string Name { get; init; }
    public double Quantity { get; init; }
    public string? Unit { get; init; }
    public string Category { get; init; }
}
```

---

## 5. LLM Router

### 5.1 Configuration

```csharp
public class LlmRouterOptions
{
    public const string SectionName = "Llm";
    
    public string DefaultProvider { get; set; } = "Claude";
    public OpenAIOptions OpenAI { get; set; } = new();
    public AnthropicOptions Anthropic { get; set; } = new();
    public GeminiOptions Gemini { get; set; } = new();
    
    public Dictionary<string, string> PhaseProviders { get; set; } = new()
    {
        ["Research"] = "Claude",
        ["Analysis"] = "OpenAI"
    };
}
```

### 5.2 Provider Configuration

| Provider | Options Class | Default Model |
|----------|---------------|---------------|
| OpenAI | `OpenAIOptions` | `gpt-4o` |
| Anthropic | `AnthropicOptions` | `claude-sonnet-4-20250514` |
| Gemini | `GeminiOptions` | `gemini-2.5-flash` |

### 5.3 Interface

```csharp
public interface ILlmRouter
{
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LlmStreamChunk> StreamChatAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
```

### 5.4 Resilience

- **Retry Strategy:** Exponential backoff with jitter
- **Max Retries:** 3
- **Base Delay:** 1 second

---

## 6. Messaging & Real-Time Updates

### 6.1 Redis Streams

Used for task queue management:
- Stream key pattern: `tasks:{AgentType}`
- Task state key pattern: `taskstate:{TaskId}`
- Default TTL: Configurable (default 60 minutes)

### 6.2 SignalR Hub

**Hub:** `AgentHub`

**Client Methods:**
- `ReceiveEvent(AgentEvent event)` - Receives progress/completion events

**Server Methods:**
- `JoinThread(string threadId)` - Join a thread group for updates
- `LeaveThread(string threadId)` - Leave a thread group

### 6.3 Event Types

| Event Type | Description |
|------------|-------------|
| `task.started` | Task processing has begun |
| `task.completed` | Task finished successfully |
| `task.failed` | Task encountered an error |
| `research.progress` | Research phase progress update |
| `analysis.progress` | Analysis phase progress update |

---

## 7. MCP Server Tools

The MCP (Model Context Protocol) server exposes tools for AI agents:

### 7.1 Recipe Tools

| Tool | Description |
|------|-------------|
| `recipe_get` | Gets a recipe by ID with full details |
| `recipe_save_notes` | Saves research notes for a session |
| `recipe_get_notes` | Gets all notes for a thread |

### 7.2 Search Tools

| Tool | Description |
|------|-------------|
| `search_recipes` | Searches recipes by query, diet, cuisine |

### 7.3 Nutrition Tools

| Tool | Description |
|------|-------------|
| `nutrition_compute` | Computes nutrition for ingredient list |

---

## 8. API Endpoints

### 8.1 Gateway REST API

#### Recipes
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/recipes` | Search recipes |
| GET | `/api/recipes/{id}` | Get recipe by ID |
| POST | `/api/recipes` | Create new recipe |

#### Tasks
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/tasks` | Create and enqueue task |
| GET | `/api/tasks/{taskId}` | Get task by ID |
| GET | `/api/tasks/{taskId}/state` | Get task state |
| POST | `/api/tasks/{taskId}/cancel` | Cancel task |
| GET | `/api/tasks/{taskId}/artifacts` | Get task artifacts |

#### Sessions
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sessions` | Create session |
| GET | `/api/sessions/{id}` | Get session |

#### Artifacts
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/artifacts/{*blobPath}` | Download artifact |

---

## 9. Data Storage

### 9.1 Cosmos DB Containers

| Container | Partition Key | Purpose |
|-----------|---------------|---------|
| `recipes` | `/id` | Recipe documents |
| `sessions` | `/threadId` | User sessions |
| `messages` | `/threadId` | Conversation messages |
| `tasks` | `/taskId` | Agent tasks |
| `artifacts` | `/taskId` | Artifact metadata |
| `notes` | `/threadId` | Research notes |

### 9.2 Blob Storage

- **Container:** `artifacts`
- **Path Pattern:** `{taskId}/{filename}`
- **Artifact Types:** Markdown recipe cards, JSON shopping lists

---

## 10. Blazor Client

### 10.1 Features

- **Recipe Search:** Natural language query input with diet/cuisine filters
- **Real-Time Progress:** SignalR-powered progress indicators
- **Candidate Display:** Ranked recipe cards with relevance scores
- **Analysis View:** Nutrition summary and shopping list
- **Artifact Downloads:** Direct download links for generated files

### 10.2 Services

| Service | Purpose |
|---------|---------|
| `ApiClientService` | HTTP client for Gateway API calls |
| `SignalRClientService` | Real-time event subscription |

---

## 11. Configuration

### 11.1 User Secrets

API keys are stored in .NET User Secrets (AppHost project):

```json
{
  "Parameters:openai-api-key": "sk-...",
  "Parameters:anthropic-api-key": "sk-ant-...",
  "Llm:Gemini:ApiKey": "AIza..."
}
```

### 11.2 Aspire Parameters

```csharp
var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);
```

---

## 12. Deployment

### 12.1 Local Development

```bash
# Start with Aspire
cd platform/AppHost
dotnet run
```

Aspire Dashboard: `https://localhost:17225`

### 12.2 Infrastructure (Emulators)

- **Cosmos DB Emulator:** Persistent data volume
- **Azure Storage Emulator:** Persistent data volume
- **Redis:** Persistent data volume

---

## 13. Future Enhancements

### 13.1 Planned: Image Generation (Gemini Imagen)

Add AI-generated dish images using Gemini's Imagen capability:

| Feature | Description |
|---------|-------------|
| Plated Dish Preview | Generate image of completed dish |
| Step Visuals | Images for key cooking steps |
| Ingredient Layout | Mise en place visualization |

**Configuration:**
```csharp
public Dictionary<string, string> PhaseProviders { get; set; } = new()
{
    ["Research"] = "Claude",
    ["Analysis"] = "OpenAI",
    ["ImageGeneration"] = "Gemini"  // Planned
};
```

---

## 14. Appendix

### 14.1 Ingredient Nutrition Estimates

The system uses simplified nutrition estimation based on ingredient keywords:

| Ingredient | Calories | Protein (g) | Carbs (g) | Fat (g) |
|------------|----------|-------------|-----------|---------|
| Chicken | 165 | 31 | 0 | 3.6 |
| Beef | 250 | 26 | 0 | 15 |
| Rice | 130 | 2.7 | 28 | 0.3 |
| Pasta | 131 | 5 | 25 | 1.1 |
| Egg | 78 | 6 | 0.6 | 5 |
| Olive Oil | 120 | 0 | 0 | 14 |

*Values are per unit quantity as defined in the recipe ingredient.*

### 14.2 Repository

- **GitHub:** `https://github.com/Rebel-Alliance-2023/cookbook-agent-platform`
- **Branch:** `main`