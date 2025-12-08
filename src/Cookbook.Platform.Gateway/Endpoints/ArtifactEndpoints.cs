using Cookbook.Platform.Storage;
using Cookbook.Platform.Storage.Repositories;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Artifact download endpoints.
/// </summary>
public static class ArtifactEndpoints
{
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/artifacts")
            .WithTags("Artifacts");

        group.MapGet("/{taskId}/{fileName}", DownloadArtifact)
            .WithName("DownloadArtifact")
            .WithSummary("Downloads an artifact file");

        group.MapGet("/{taskId}", ListArtifacts)
            .WithName("ListArtifacts")
            .WithSummary("Lists all artifacts for a task");

        return endpoints;
    }

    private static async Task<IResult> DownloadArtifact(
        string taskId,
        string fileName,
        IBlobStorage blobStorage,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = $"{taskId}/{fileName}";
            var content = await blobStorage.DownloadAsync(path, cancellationToken);
            
            // Determine content type from file extension
            var contentType = fileName.EndsWith(".md") ? "text/markdown" :
                              fileName.EndsWith(".json") ? "application/json" :
                              "application/octet-stream";

            return Results.File(content, contentType, fileName);
        }
        catch (Exception)
        {
            return Results.NotFound($"Artifact {fileName} not found for task {taskId}");
        }
    }

    private static async Task<IResult> ListArtifacts(
        string taskId,
        ArtifactRepository artifactRepository,
        CancellationToken cancellationToken)
    {
        var artifacts = await artifactRepository.GetByTaskIdAsync(taskId, cancellationToken);
        
        return Results.Ok(artifacts.Select(a => new
        {
            a.Name,
            a.ContentType,
            a.SizeBytes,
            a.CreatedAt,
            DownloadUrl = $"/api/artifacts/{taskId}/{a.Name}"
        }));
    }
}
