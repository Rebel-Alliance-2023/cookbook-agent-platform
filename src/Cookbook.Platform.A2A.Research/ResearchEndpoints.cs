using Cookbook.Platform.A2A.Research.Services;

namespace Cookbook.Platform.A2A.Research;

/// <summary>
/// Endpoint mappings for the Research Agent.
/// </summary>
public static class ResearchEndpoints
{
    public static IEndpointRouteBuilder MapResearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/research")
            .WithTags("Research Agent");

        group.MapPost("/", ExecuteResearch)
            .WithName("ExecuteResearch")
            .WithSummary("Executes the research phase for a task");

        group.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Agent = "Research" }))
            .WithName("ResearchHealth")
            .WithSummary("Health check for the research agent");

        return endpoints;
    }

    private static async Task<IResult> ExecuteResearch(
        ResearchRequest request,
        ResearchAgentServer agent,
        CancellationToken cancellationToken)
    {
        var result = await agent.ExecuteAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}
