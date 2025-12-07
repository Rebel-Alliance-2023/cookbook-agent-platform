# Cookbook Agent Platform

A .NET Aspire-based agentic cookbook platform demonstrating A2A (Agent-to-Agent) and MCP (Model Context Protocol) patterns for recipe research, analysis, and artifact generation.

## 🎯 Overview

This platform showcases:
- **A2A Protocol**: Agent collaboration for research and analysis phases
- **MCP Protocol**: Deterministic tools for recipe search, nutrition computation, and file operations
- **Real-time UX**: SignalR-based streaming of agent progress and events
- **Semi-durable State**: Redis with TTL for transient state
- **Durable Persistence**: Cosmos DB for recipes, sessions, and artifacts
- **Multi-LLM Support**: Configurable routing between OpenAI and Anthropic

## 📁 Project Structure

```
cookbook-agent-platform/
├── platform/
│   ├── AppHost/              # Aspire orchestration
│   └── ServiceDefaults/      # Shared OpenTelemetry, health checks
├── src/
│   ├── Cookbook.Platform.Shared/         # Core abstractions and models
│   ├── Cookbook.Platform.Infrastructure/ # Messaging, LLM router
│   ├── Cookbook.Platform.Storage/        # Cosmos DB and Blob repositories
│   ├── Cookbook.Platform.Gateway/        # API gateway with SignalR hub
│   ├── Cookbook.Platform.Orchestrator/   # Pipeline coordination
│   ├── Cookbook.Platform.Mcp/            # MCP server with tools
│   ├── Cookbook.Platform.A2A.Research/   # Research agent
│   ├── Cookbook.Platform.A2A.Analysis/   # Analysis agent
│   └── Cookbook.Platform.Client.Blazor/  # Blazor Server UI
├── data/
│   └── seed-recipes.json     # Sample recipe data
├── docs/
│   └── Agentic DotNet Architecture.md
└── tests/                    # Unit and integration tests
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Visual Studio 2022 17.12+](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Running Locally

1. **Clone the repository**
   ```bash
   git clone https://github.com/Rebel-Alliance-2023/cookbook-agent-platform.git
   cd cookbook-agent-platform
   ```

2. **Set up user secrets** (for LLM API keys)
   ```bash
   cd platform/AppHost
   dotnet user-secrets set "Parameters:openai-api-key" "your-openai-key"
   dotnet user-secrets set "Parameters:anthropic-api-key" "your-anthropic-key"
   ```

3. **Run the Aspire AppHost**
   ```bash
   dotnet run --project platform/AppHost
   ```

4. **Access the services**
   - Aspire Dashboard: https://localhost:17082
   - Blazor Client: https://localhost:5001
   - Gateway API: https://localhost:5002
   - Swagger UI: https://localhost:5002/swagger

### LLM API Keys

You need API keys from at least one provider:

| Provider | Get API Key | Used By |
|----------|-------------|---------|
| **OpenAI** | https://platform.openai.com/api-keys | Analysis Agent (gpt-4o) |
| **Anthropic** | https://console.anthropic.com/settings/keys | Research Agent (claude-3-5-sonnet) |

### Docker Resources

The AppHost automatically provisions:
- **Redis**: For SignalR backplane and semi-durable state
- **Cosmos Emulator**: For recipe, session, and artifact storage
- **Azurite**: For blob storage of generated artifacts

## 🔄 Startup Flow

When you run the AppHost, services start in the following order with automatic dependency management:

```
AppHost starts
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  Infrastructure Services (parallel)                  │
│  ├── Redis container starts                         │
│  ├── Cosmos DB Emulator starts                      │
│  └── Azurite (Azure Storage Emulator) starts        │
└─────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  MCP Server starts                                   │
│  ├── DatabaseInitializer runs                       │
│  │   ├── Creates Cosmos DB database (cookbook-db)   │
│  │   ├── Creates all 6 containers                   │
│  │   └── Seeds sample recipes (if empty)            │
│  └── MCP tools become available                     │
└─────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  A2A Agents start (parallel, wait for MCP)          │
│  ├── Research Agent (uses Claude)                   │
│  └── Analysis Agent (uses OpenAI)                   │
└─────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  Orchestrator starts (waits for agents)             │
│  └── Coordinates agent pipelines                    │
└─────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  Gateway API starts (waits for orchestrator)        │
│  ├── REST API endpoints                             │
│  └── SignalR hub for real-time events               │
└─────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────┐
│  Blazor Client starts (waits for gateway)           │
│  └── Web UI ready for users                         │
└─────────────────────────────────────────────────────┘
```

### Database Initialization

On first startup, the `DatabaseInitializer` automatically:

1. **Creates the database** (`cookbook-db`) if it doesn't exist
2. **Creates all required containers** with proper partition keys:
   | Container | Partition Key |
   |-----------|---------------|
   | recipes | /id |
   | sessions | /threadId |
   | messages | /threadId |
   | tasks | /taskId |
   | artifacts | /taskId |
   | notes | /threadId |

3. **Seeds sample recipes** from `data/seed-recipes.json` (if container is empty)

## ⚙️ Architecture

### Service Communication

```
┌─────────────┐     ┌─────────────┐     ┌────────────────┐
│   Blazor    │────▶│   Gateway   │────▶│  Orchestrator  │
│   Client    │◀────│  (SignalR)  │◀────│                │
└─────────────┘     └─────────────┘     └────────────────┘
                                               │
                          ┌────────────────────┼────────────────────┐
                          │                    │                    │
                   ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
                   │  Research   │     │  Analysis   │     │    MCP      │
                   │   Agent     │     │   Agent     │     │   Server    │
                   └─────────────┘     └─────────────┘     └─────────────┘
```

### MCP Tools

| Tool | Description |
|------|-------------|
| `search_recipes` | Search recipes by query, diet, cuisine |
| `recipe_get` | Get full recipe details by ID |
| `recipe_save_notes` | Save research notes for a session |
| `nutrition_compute` | Estimate nutrition for ingredients |
| `files_put` | Upload artifacts to blob storage |
| `files_list` | List stored artifacts |

### A2A Agents

- **Research Agent**: Discovers recipe candidates, scores relevance using Claude, saves notes
- **Analysis Agent**: Normalizes recipes, computes nutrition, generates shopping lists, creates artifacts

## 🔧 Configuration

### Development (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Cookbook.Platform": "Debug"
    }
  },
  "ConnectionStrings": {
    "redis": "localhost:6379",
    "cosmos": "AccountEndpoint=https://localhost:8081/;AccountKey=...",
    "blobs": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;..."
  },
  "MessagingBus": {
    "Implementation": "RedisSignalR",
    "RedisStreamPrefix": "stream:",
    "RedisKeyPrefix": "runs:",
    "RedisDb": 0,
    "DefaultTtlMinutes": 60
  },
  "Cosmos": {
    "DatabaseName": "cookbook-db",
    "UseEmulator": true
  },
  "Llm": {
    "DefaultProvider": "Claude",
    "OpenAI": { "Model": "gpt-4o" },
    "Anthropic": { "Model": "claude-3-5-sonnet-20241022" }
  }
}
```

### User Secrets (API Keys)

API keys are stored securely using .NET User Secrets:

```bash
cd platform/AppHost
dotnet user-secrets set "Parameters:openai-api-key" "sk-..."
dotnet user-secrets set "Parameters:anthropic-api-key" "sk-ant-..."
dotnet user-secrets list  # Verify
```

### Production

In production, secrets are managed via Azure Key Vault and accessed using managed identity.

## 📊 Observability

- **OpenTelemetry**: Distributed tracing across all services
- **Aspire Dashboard**: Local development monitoring
- **Azure Monitor**: Cloud deployment monitoring (configured in ServiceDefaults)

## 📝 Data Model

### Cosmos DB Containers

| Container | Partition Key | Purpose |
|-----------|--------------|---------|
| recipes | /id | Recipe storage |
| sessions | /threadId | User sessions |
| messages | /threadId | Conversation history |
| tasks | /taskId | Agent task tracking |
| artifacts | /taskId | Generated artifact metadata |
| notes | /threadId | Research notes |

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🌐 Deployment

### Azure Container Apps

```bash
# Build and push to ACR
az acr build -t cookbook-platform:latest -r yourregistry .

# Deploy using Aspire manifest
dotnet run --project platform/AppHost -- --publisher manifest --output-path ./manifest.json
```

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

Login to the dashboard at https://localhost:17082/login?t=<token>

