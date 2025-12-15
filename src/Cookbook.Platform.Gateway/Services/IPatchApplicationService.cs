using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Gateway.Services;

/// <summary>
/// Service for applying and rejecting normalization patches.
/// </summary>
public interface IPatchApplicationService
{
    /// <summary>
    /// Applies normalization patches to a recipe.
    /// </summary>
    Task<PatchApplicationResult> ApplyPatchAsync(
        string recipeId, 
        ApplyPatchRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects normalization patches for a recipe.
    /// </summary>
    Task<PatchRejectionResult> RejectPatchAsync(
        string recipeId, 
        RejectPatchRequest request, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of applying patches.
/// </summary>
public record PatchApplicationResult
{
    public required int StatusCode { get; init; }
    public ApplyPatchResponse? Response { get; init; }
    public PatchApplicationError? Error { get; init; }
}

/// <summary>
/// Error information for patch application.
/// </summary>
public record PatchApplicationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? TaskId { get; init; }
    public string? RecipeId { get; init; }
}

/// <summary>
/// Result of rejecting patches.
/// </summary>
public record PatchRejectionResult
{
    public required int StatusCode { get; init; }
    public RejectPatchResponse? Response { get; init; }
    public PatchApplicationError? Error { get; init; }
}
