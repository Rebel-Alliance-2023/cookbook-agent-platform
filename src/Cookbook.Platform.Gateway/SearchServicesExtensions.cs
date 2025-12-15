using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cookbook.Platform.Gateway;

/// <summary>
/// Extension methods for registering search provider services.
/// </summary>
public static class SearchServicesExtensions
{
    /// <summary>
    /// Adds search provider services to the service collection.
    /// </summary>
    public static IServiceCollection AddSearchProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind configuration options
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
        services.Configure<BraveSearchOptions>(configuration.GetSection(BraveSearchOptions.SectionName));
        services.Configure<GoogleSearchOptions>(configuration.GetSection(GoogleSearchOptions.SectionName));

        // Register HttpClient for providers
        services.AddHttpClient<BraveSearchProvider>();
        services.AddHttpClient<GoogleCustomSearchProvider>();

        // Register providers as ISearchProvider
        services.AddSingleton<ISearchProvider, BraveSearchProvider>();
        services.AddSingleton<ISearchProvider, GoogleCustomSearchProvider>();

        // Register the resolver
        services.AddSingleton<ISearchProviderResolver, SearchProviderResolver>();

        return services;
    }
}
