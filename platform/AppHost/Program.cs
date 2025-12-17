using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// LLM API Keys (from user secrets)
var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);

// ============================================
// LAYER 1: Infrastructure Resources
// These must be ready before any services start
// ============================================

var redis = builder.AddRedis("redis")
    .WithDataVolume("cookbook-redis-data");

// Cosmos emulator can take 2-3 minutes to fully initialize on first run
// Using persistent lifetime to preserve data and speed up subsequent starts
var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume("cookbook-cosmos-data")
        .WithLifetime(ContainerLifetime.Persistent));

var cosmosDb = cosmos.AddCosmosDatabase("cookbook-db");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume("cookbook-storage-data")
        .WithLifetime(ContainerLifetime.Persistent));

var blobs = storage.AddBlobs("blobs");

// ============================================
// LAYER 2: MCP Server (database seeding)
// Waits for all infrastructure
// ============================================

var mcpServer = builder.AddProject<Projects.Cookbook_Platform_Mcp>("mcp-server")
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithReference(redis)
    .WaitFor(redis)
    .WaitFor(cosmos)
    .WaitFor(storage);

// ============================================
// LAYER 3: A2A Agents
// Wait for MCP to seed database (cosmos already waited on by MCP)
// ============================================

var researchAgent = builder.AddProject<Projects.Cookbook_Platform_A2A_Research>("research-agent")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WithExternalHttpEndpoints()
    .WaitFor(mcpServer);  // MCP already waited for all infrastructure

var analysisAgent = builder.AddProject<Projects.Cookbook_Platform_A2A_Analysis>("analysis-agent")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WithExternalHttpEndpoints()
    .WaitFor(mcpServer);  // MCP already waited for all infrastructure

// ============================================
// LAYER 4: Orchestrator Service
// Waits for both A2A agents to be ready
// ============================================

var orchestrator = builder.AddProject<Projects.Cookbook_Platform_Orchestrator>("orchestrator")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithReference(researchAgent)
    .WithReference(analysisAgent)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WaitFor(researchAgent)
    .WaitFor(analysisAgent);

// ============================================
// LAYER 5: Gateway API
// Waits for orchestrator to be fully ready
// ============================================

var gateway = builder.AddProject<Projects.Cookbook_Platform_Gateway>("gateway")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithReference(orchestrator)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WithExternalHttpEndpoints()
    .WaitFor(orchestrator);

// ============================================
// LAYER 6: Blazor Client
// Reference gateway for service discovery
// ============================================

builder.AddProject<Projects.Cookbook_Platform_Client_Blazor>("blazor-client")
    .WithReference(gateway)
    .WithExternalHttpEndpoints()
    .WaitFor(gateway);

builder.Build().Run();
