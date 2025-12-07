using Cookbook.Platform.A2A.Analysis;
using Cookbook.Platform.A2A.Analysis.Services;
using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Storage;

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

// Add analysis agent service
builder.Services.AddSingleton<AnalysisAgentServer>();

var app = builder.Build();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Map analysis agent endpoints
app.MapAnalysisEndpoints();

app.Run();
