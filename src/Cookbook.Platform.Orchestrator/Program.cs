using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Orchestrator.Services;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
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

// Add Ingest options
builder.Services.Configure<IngestOptions>(
    builder.Configuration.GetSection(IngestOptions.SectionName));
builder.Services.Configure<IngestGuardrailOptions>(
    builder.Configuration.GetSection(IngestGuardrailOptions.SectionName));

// Add orchestrator services
builder.Services.AddSingleton<OrchestratorService>();
builder.Services.AddSingleton<AgentPipeline>();
builder.Services.AddSingleton<IngestPhaseRunner>();

// Add Ingest services
builder.Services.AddSingleton<ISsrfProtectionService, SsrfProtectionService>();
builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
builder.Services.AddSingleton<ISanitizationService, HtmlSanitizationService>();
builder.Services.AddHttpClient<IFetchService, HttpFetchService>();

// Add Recipe Extraction services
builder.Services.AddSingleton<JsonLdRecipeExtractor>();
builder.Services.AddSingleton<LlmRecipeExtractor>();
builder.Services.AddSingleton<RecipeExtractionOrchestrator>();

// Add Recipe Validation services
builder.Services.AddSingleton<IRecipeValidator, RecipeValidator>();

// Add Similarity Detection services
builder.Services.Configure<SimilarityOptions>(
    builder.Configuration.GetSection("Ingest:Similarity"));
builder.Services.AddSingleton<ISimilarityDetector, SimilarityDetector>();

// Add Repair Paraphrase services
builder.Services.AddSingleton<IRepairParaphraseService, RepairParaphraseService>();

// Add Normalize services
builder.Services.AddSingleton<INormalizeService, NormalizeService>();

// Add Artifact Storage services
builder.Services.AddSingleton<IArtifactStorageService, BlobArtifactStorageService>();

// Add background services
builder.Services.AddHostedService<TaskProcessorService>();
builder.Services.AddHostedService<DraftExpirationService>();

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
