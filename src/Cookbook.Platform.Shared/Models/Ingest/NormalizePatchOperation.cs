using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Risk category for a normalize patch operation.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum NormalizePatchRiskCategory
{
    /// <summary>
    /// Safe formatting changes (capitalization, punctuation, unit standardization).
    /// </summary>
    Low,
    
    /// <summary>
    /// Data modifications that preserve meaning (ingredient parsing, metadata inference).
    /// </summary>
    Medium,
    
    /// <summary>
    /// Content changes affecting cooking (instruction modifications, error corrections).
    /// </summary>
    High
}

/// <summary>
/// JSON Patch operation type.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum JsonPatchOperationType
{
    /// <summary>
    /// Replace an existing value.
    /// </summary>
    [JsonPropertyName("replace")]
    Replace,
    
    /// <summary>
    /// Add a new value.
    /// </summary>
    [JsonPropertyName("add")]
    Add,
    
    /// <summary>
    /// Remove an existing value.
    /// </summary>
    [JsonPropertyName("remove")]
    Remove
}

/// <summary>
/// Represents a single normalize patch operation following JSON Patch RFC 6902 with additional metadata.
/// </summary>
public record NormalizePatchOperation
{
    /// <summary>
    /// The operation type (replace, add, remove).
    /// </summary>
    [JsonPropertyName("op")]
    [JsonProperty("op")]
    public required JsonPatchOperationType Op { get; init; }

    /// <summary>
    /// The JSON Pointer path to the value being modified.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonProperty("path")]
    public required string Path { get; init; }

    /// <summary>
    /// The new value for replace/add operations.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonProperty("value")]
    public object? Value { get; init; }

    /// <summary>
    /// The risk category for this operation.
    /// </summary>
    [JsonPropertyName("riskCategory")]
    [JsonProperty("riskCategory")]
    public required NormalizePatchRiskCategory RiskCategory { get; init; }

    /// <summary>
    /// Explanation for why this change is suggested.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonProperty("reason")]
    public required string Reason { get; init; }

    /// <summary>
    /// The original value before the change (populated during test-apply).
    /// </summary>
    [JsonPropertyName("originalValue")]
    [JsonProperty("originalValue")]
    public object? OriginalValue { get; init; }
}

/// <summary>
/// Response from the LLM containing normalize patch operations.
/// </summary>
public record NormalizePatchResponse
{
    /// <summary>
    /// The list of patch operations to apply.
    /// </summary>
    [JsonPropertyName("patches")]
    [JsonProperty("patches")]
    public IReadOnlyList<NormalizePatchOperation> Patches { get; init; } = [];

    /// <summary>
    /// Brief summary of all changes.
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonProperty("summary")]
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Whether any high-risk changes are included.
    /// </summary>
    [JsonPropertyName("hasHighRiskChanges")]
    [JsonProperty("hasHighRiskChanges")]
    public bool HasHighRiskChanges { get; init; }

    /// <summary>
    /// Number of low-risk patches.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public int LowRiskCount => Patches.Count(p => p.RiskCategory == NormalizePatchRiskCategory.Low);

    /// <summary>
    /// Number of medium-risk patches.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public int MediumRiskCount => Patches.Count(p => p.RiskCategory == NormalizePatchRiskCategory.Medium);

    /// <summary>
    /// Number of high-risk patches.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public int HighRiskCount => Patches.Count(p => p.RiskCategory == NormalizePatchRiskCategory.High);
}

/// <summary>
/// Result of applying a normalize patch to a recipe.
/// </summary>
public record NormalizePatchResult
{
    /// <summary>
    /// Whether all patches were applied successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The patches that were applied.
    /// </summary>
    public IReadOnlyList<NormalizePatchOperation> AppliedPatches { get; init; } = [];

    /// <summary>
    /// Patches that failed to apply with error messages.
    /// </summary>
    public IReadOnlyList<NormalizePatchError> FailedPatches { get; init; } = [];

    /// <summary>
    /// The normalized recipe after applying patches.
    /// </summary>
    public Recipe? NormalizedRecipe { get; init; }

    /// <summary>
    /// Summary of the normalization.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static NormalizePatchResult Succeeded(Recipe normalizedRecipe, IReadOnlyList<NormalizePatchOperation> appliedPatches, string summary) => new()
    {
        Success = true,
        NormalizedRecipe = normalizedRecipe,
        AppliedPatches = appliedPatches,
        Summary = summary
    };

    /// <summary>
    /// Creates a partially successful result.
    /// </summary>
    public static NormalizePatchResult Partial(
        Recipe normalizedRecipe, 
        IReadOnlyList<NormalizePatchOperation> appliedPatches, 
        IReadOnlyList<NormalizePatchError> failedPatches,
        string summary) => new()
    {
        Success = false,
        NormalizedRecipe = normalizedRecipe,
        AppliedPatches = appliedPatches,
        FailedPatches = failedPatches,
        Summary = summary
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static NormalizePatchResult Failed(string error) => new()
    {
        Success = false,
        Summary = error
    };
}

/// <summary>
/// Represents a patch that failed to apply.
/// </summary>
public record NormalizePatchError
{
    /// <summary>
    /// The patch operation that failed.
    /// </summary>
    public required NormalizePatchOperation Patch { get; init; }

    /// <summary>
    /// The error message.
    /// </summary>
    public required string Error { get; init; }
}
