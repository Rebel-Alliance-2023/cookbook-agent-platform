using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// LLM API Keys (from user secrets)
var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);

// Infrastructure resources
var redis = builder.AddRedis("redis")
    .WithDataVolume("cookbook-redis-data");

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

// MCP Server - Cookbook Tools (handles database seeding)
var mcpServer = builder.AddProject<Projects.Cookbook_Platform_Mcp>("mcp-server")
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithReference(redis)
    .WaitFor(redis)
    .WaitFor(cosmos)
    .WaitFor(storage);

// A2A Agents - wait for infrastructure (not MCP, since agents don't call MCP directly)
var researchAgent = builder.AddProject<Projects.Cookbook_Platform_A2A_Research>("research-agent")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WithExternalHttpEndpoints()
    .WaitFor(redis)
    .WaitFor(cosmos)
    .WaitFor(mcpServer);  // Wait for MCP to seed the database

var analysisAgent = builder.AddProject<Projects.Cookbook_Platform_A2A_Analysis>("analysis-agent")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithEnvironment("Llm__OpenAI__ApiKey", openAiApiKey)
    .WithEnvironment("Llm__Anthropic__ApiKey", anthropicApiKey)
    .WithExternalHttpEndpoints()
    .WaitFor(redis)
    .WaitFor(cosmos)
    .WaitFor(storage)
    .WaitFor(mcpServer);  // Wait for MCP to seed the database

// Orchestrator Service
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

// Gateway API
var gateway = builder.AddProject<Projects.Cookbook_Platform_Gateway>("gateway")
    .WithReference(redis)
    .WithReference(cosmosDb)
    .WithReference(blobs)
    .WithReference(orchestrator)
    .WithExternalHttpEndpoints()
    .WaitFor(orchestrator);

// Blazor Client
builder.AddProject<Projects.Cookbook_Platform_Client_Blazor>("blazor-client")
    .WithReference(gateway)
    .WithExternalHttpEndpoints()
    .WaitFor(gateway);

builder.Build().Run();
