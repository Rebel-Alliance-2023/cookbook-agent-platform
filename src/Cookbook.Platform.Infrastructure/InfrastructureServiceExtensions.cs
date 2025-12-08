using Cookbook.Platform.Infrastructure.Llm;
using Cookbook.Platform.Infrastructure.Messaging;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cookbook.Platform.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds messaging bus services to the service collection.
    /// </summary>
    public static IServiceCollection AddMessagingBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingBusOptions>(configuration.GetSection(MessagingBusOptions.SectionName));
        
        // Register SignalR with Redis backplane
        services.AddSignalR()
            .AddStackExchangeRedis();

        // Register the messaging bus implementation
        services.AddSingleton<IMessagingBus, RedisSignalRMessagingBus>();

        return services;
    }

    /// <summary>
    /// Adds LLM router services to the service collection.
    /// </summary>
    public static IServiceCollection AddLlmRouter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmRouterOptions>(configuration.GetSection(LlmRouterOptions.SectionName));
        
        // Register a dedicated HttpClient for LLM calls with extended timeouts
        // (LLM calls can take 30+ seconds, default Polly timeouts are too aggressive)
        services.AddHttpClient("LlmClient", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
        .RemoveAllLoggers(); // Reduce noise from LLM HTTP calls
        
        services.AddSingleton<ILlmRouter, LlmRouter>();

        return services;
    }

    /// <summary>
    /// Adds LLM router services with health check (shows in Aspire Dashboard).
    /// </summary>
    public static IServiceCollection AddLlmRouterWithHealthCheck(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLlmRouter(configuration);
        
        // Add health check that validates LLM configuration
        services.AddHealthChecks()
            .AddCheck<LlmHealthCheck>("llm-providers", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps the SignalR agent hub endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapAgentHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<AgentHub>("/agentHub");
        return endpoints;
    }
}
