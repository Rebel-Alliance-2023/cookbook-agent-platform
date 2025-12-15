using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Request to apply normalization patches to a recipe.
/// </summary>
public record ApplyPatchRequest
{
    /// <summary>
    /// The task ID that generated the patches.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// Indices of patches to apply (0-based). If null or empty, all patches are applied.
    /// </summary>
    [JsonPropertyName("patchIndices")]
    [JsonProperty("patchIndices")]
    public IReadOnlyList<int>? PatchIndices { get; init; }

    /// <summary>
    /// Whether to only apply patches up to and including the specified max risk level.
    /// </summary>
    [JsonPropertyName("maxRiskLevel")]
    [JsonProperty("maxRiskLevel")]
    public NormalizePatchRiskCategory? MaxRiskLevel { get; init; }

    /// <summary>
    /// Optional reason for applying the patches (for audit log).
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonProperty("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Response from applying normalization patches.
/// </summary>
public record ApplyPatchResponse
{
    /// <summary>
    /// Whether all requested patches were applied successfully.
    /// </summary>
    [JsonPropertyName("success")]
    [JsonProperty("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// The updated recipe ID.
    /// </summary>
    [JsonPropertyName("reciped")]
    [JsonProperty("recipeId")]
    public required string RecipeId { get; init; }

    /// <summary>
    /// Number of patches applied.
    /// </summary>
    [JsonPropertyName("appliedCount")]
    [JsonProperty("appliedCount")]
    public int AppliedCount { get; init; }

    /// <summary>
    /// Number of patches that failed to apply.
    /// </summary>
    [JsonPropertyName("failedCount")]
    [JsonProperty("failedCount")]
    public int FailedCount { get; init; }

    /// <summary>
    /// Number of patches skipped (filtered by risk level or indices).
    /// </summary>
    [JsonPropertyName("skippedCount")]
    [JsonProperty("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>
    /// Summary message describing the result.
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonProperty("summary")]
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Error details if any patches failed.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonProperty("errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// The new ETag for the updated recipe.
    /// </summary>
    [JsonPropertyName("etag")]
    [JsonProperty("etag")]
    public string? ETag { get; init; }
}

/// <summary>
/// Request to reject normalization patches.
/// </summary>
public record RejectPatchRequest
{
    /// <summary>
    /// The task ID that generated the patches.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// Reason for rejecting the patches (for audit log).
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonProperty("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Response from rejecting normalization patches.
/// </summary>
public record RejectPatchResponse
{
    /// <summary>
    /// Whether the rejection was successful.
    /// </summary>
    [JsonPropertyName("success")]
    [JsonProperty("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// The recipe ID that was left unchanged.
    /// </summary>
    [JsonPropertyName("recipeId")]
    [JsonProperty("recipeId")]
    public required string RecipeId { get; init; }

    /// <summary>
    /// Summary message.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public string Message { get; init; } = string.Empty;
}
