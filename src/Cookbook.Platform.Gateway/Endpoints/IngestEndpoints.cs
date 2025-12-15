using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Models.Ingest.Search;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Endpoints for the Recipe Ingest Agent.
/// </summary>
public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ingest")
            .WithTags("Ingest");

        group.MapGet("/providers/search", GetSearchProviders)
            .WithName("GetSearchProviders")
            .WithSummary("Gets available search providers for recipe discovery")
            .WithDescription("Returns the list of enabled search providers and the default provider ID. " +
                           "Use this to populate the provider selector in the UI.");

        return endpoints;
    }

    /// <summary>
    /// Gets the list of available search providers.
    /// </summary>
    private static IResult GetSearchProviders(ISearchProviderResolver resolver)
    {
        var enabledProviders = resolver.ListEnabled();

        var response = new SearchProvidersResponse
        {
            DefaultProviderId = resolver.DefaultProviderId,
            Providers = enabledProviders
        };

        return Results.Ok(response);
    }
}
