using Cookbook.Platform.A2A.Research;
using Cookbook.Platform.A2A.Research.Services;
using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Redis
builder.AddRedisClient("redis");

// Add Cosmos DB
builder.AddAzureCosmosClient("cosmos");

// Add infrastructure services
builder.Services.AddMessagingBus(builder.Configuration);
builder.Services.AddLlmRouter(builder.Configuration);

// Add storage services
builder.Services.AddCosmosRepositories(builder.Configuration);

// Add research agent service
builder.Services.AddSingleton<ResearchAgentServer>();

var app = builder.Build();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Map research agent endpoints
app.MapResearchEndpoints();

app.Run();
