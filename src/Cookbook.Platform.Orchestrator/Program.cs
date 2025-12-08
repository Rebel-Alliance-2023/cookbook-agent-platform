using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Orchestrator.Services;
using Cookbook.Platform.Storage;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Redis
builder.AddRedisClient("redis");

// Add Cosmos DB
builder.AddAzureCosmosClient("cosmos");

// Add Blob Storage
builder.AddAzureBlobClient("blobs");

// Add infrastructure services
builder.Services.AddMessagingBus(builder.Configuration);
builder.Services.AddLlmRouter(builder.Configuration);

// Add storage services
builder.Services.AddCosmosRepositories(builder.Configuration);
builder.Services.AddBlobStorage(builder.Configuration);

// Add orchestrator services
builder.Services.AddSingleton<OrchestratorService>();
builder.Services.AddSingleton<AgentPipeline>();
builder.Services.AddHostedService<TaskProcessorService>();

// Add HTTP clients for A2A agents with service discovery
builder.Services.AddHttpClient("ResearchAgent", client =>
{
    client.BaseAddress = new Uri("http://research-agent");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddServiceDiscovery();

builder.Services.AddHttpClient("AnalysisAgent", client =>
{
    client.BaseAddress = new Uri("http://analysis-agent");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddServiceDiscovery();

var app = builder.Build();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Map SignalR hub for orchestrator
app.MapAgentHub();

app.Run();
