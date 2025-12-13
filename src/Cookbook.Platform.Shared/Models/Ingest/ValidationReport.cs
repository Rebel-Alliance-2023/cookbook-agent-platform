using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Represents the validation results for a recipe draft.
/// </summary>
public record ValidationReport
{
    /// <summary>
    /// List of validation errors that prevent the recipe from being committed.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonProperty("errors")]
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// List of validation warnings that should be reviewed but don't block commit.
    /// </summary>
    [JsonPropertyName("warnings")]
    [JsonProperty("warnings")]
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Indicates whether the recipe is valid (no errors).
    /// </summary>
    [JsonPropertyName("isValid")]
    [JsonProperty("isValid")]
    public bool IsValid => Errors.Count == 0;
}
