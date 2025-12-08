using Cookbook.Platform.A2A.Research;
using Cookbook.Platform.A2A.Research.Services;
using Cookbook.Platform.Infrastructure;
using Cookbook.Platform.Storage;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

Console.WriteLine("Research Agent: Starting...");

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("Research Agent: WebApplicationBuilder created");

// Add Aspire service defaults
builder.AddServiceDefaults();
Console.WriteLine("Research Agent: ServiceDefaults added");

// Add Redis
builder.AddRedisClient("redis");
Console.WriteLine("Research Agent: Redis client added");

// Add Cosmos DB
builder.AddAzureCosmosClient("cosmos");
Console.WriteLine("Research Agent: Cosmos client added");

// Add infrastructure services
builder.Services.AddMessagingBus(builder.Configuration);
Console.WriteLine("Research Agent: MessagingBus added");

// Add LLM router with health check (shows in Aspire Dashboard)
builder.Services.AddLlmRouterWithHealthCheck(builder.Configuration);
Console.WriteLine("Research Agent: LlmRouter added with health check");

// Add storage services
builder.Services.AddCosmosRepositories(builder.Configuration);
Console.WriteLine("Research Agent: CosmosRepositories added");

// Add research agent service
builder.Services.AddSingleton<ResearchAgentServer>();
Console.WriteLine("Research Agent: ResearchAgentServer added");

Console.WriteLine("Research Agent: Building app...");
var app = builder.Build();
Console.WriteLine("Research Agent: App built successfully");

// Map default endpoints (health checks)
app.MapDefaultEndpoints();
Console.WriteLine("Research Agent: Default endpoints mapped");

// Map research agent endpoints
app.MapResearchEndpoints();
Console.WriteLine("Research Agent: Research endpoints mapped");

// Log the actual server listen addresses once started
var logger = app.Services.GetRequiredService<ILogger<Program>>();
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var server = app.Services.GetService<IServer>();
        var addressesFeature = server?.Features.Get<IServerAddressesFeature>();
        if (addressesFeature != null)
        {
            logger.LogInformation("Research agent listening on: {Addresses}", string.Join(", ", addressesFeature.Addresses));
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to enumerate server addresses");
    }
});

Console.WriteLine("Research Agent: Calling app.Run()...");
app.Run();
