using Cookbook.Platform.Shared.Prompts;
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

// Add storage services with database initialization (creates containers and seeds data)
builder.Services.AddCosmosRepositoriesWithInitialization(builder.Configuration);
builder.Services.AddBlobStorage(builder.Configuration);

// Add prompt renderer for prompt tools
builder.Services.AddSingleton<IPromptRenderer, ScribanPromptRenderer>();

// Add MCP server with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Map MCP endpoint
app.MapMcp();

app.Run();

