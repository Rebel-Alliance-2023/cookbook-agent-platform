using Cookbook.Platform.Gateway;
using Cookbook.Platform.Gateway.Endpoints;
using Cookbook.Platform.Gateway.Services;
using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Prompts;
using Cookbook.Platform.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Redis
builder.AddRedisClient("redis");

// Add Cosmos DB
builder.AddAzureCosmosClient("cosmos");

// Add Blob Storage for artifact downloads
builder.AddAzureBlobClient("blobs");

// Add configuration options
builder.Services.AddIngestOptions(builder.Configuration);

// Add infrastructure services
builder.Services.AddMessagingBus(builder.Configuration);
builder.Services.AddLlmRouter(builder.Configuration);

// Add storage services
builder.Services.AddCosmosRepositories(builder.Configuration);
builder.Services.AddBlobStorage(builder.Configuration);

// Add search providers
builder.Services.AddSearchProviders(builder.Configuration);

// Add prompt rendering
builder.Services.AddSingleton<IPromptRenderer, ScribanPromptRenderer>();

// Add Orchestrator services needed by Gateway
builder.Services.AddSingleton<INormalizeService, NormalizeService>();

// Add Similarity detection for repair
builder.Services.Configure<SimilarityOptions>(
    builder.Configuration.GetSection("Ingest:Similarity"));
builder.Services.AddSingleton<ISimilarityDetector, SimilarityDetector>();
builder.Services.AddSingleton<IRepairParaphraseService, RepairParaphraseService>();

// Add Gateway services
builder.Services.AddScoped<IRecipeImportService, RecipeImportService>();
builder.Services.AddScoped<ITaskRejectService, TaskRejectService>();
builder.Services.AddScoped<IPatchApplicationService, PatchApplicationService>();
builder.Services.AddScoped<IRecipeRepairService, RecipeRepairService>();

// Add CORS for Blazor client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Map SignalR hub
app.MapAgentHub();

// Map API endpoints
app.MapSessionEndpoints();
app.MapTaskEndpoints();
app.MapRecipeEndpoints();
app.MapArtifactEndpoints();
app.MapPromptEndpoints();
app.MapIngestEndpoints();

app.Run();

