using Cookbook.Platform.A2A.Analysis.Services;

namespace Cookbook.Platform.A2A.Analysis;

/// <summary>
/// Endpoint mappings for the Analysis Agent.
/// </summary>
public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/analyze")
            .WithTags("Analysis Agent");

        group.MapPost("/", ExecuteAnalysis)
            .WithName("ExecuteAnalysis")
            .WithSummary("Executes the analysis phase for a task");

        group.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Agent = "Analysis" }))
            .WithName("AnalysisHealth")
            .WithSummary("Health check for the analysis agent");

        return endpoints;
    }

    private static async Task<IResult> ExecuteAnalysis(
        AnalysisRequest request,
        AnalysisAgentServer agent,
        CancellationToken cancellationToken)
    {
        var result = await agent.ExecuteAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}
