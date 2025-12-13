using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Represents a draft recipe that is pending review before being committed.
/// Wraps the extracted recipe with validation results and provenance information.
/// </summary>
public record RecipeDraft
{
    /// <summary>
    /// The extracted recipe data.
    /// </summary>
    [JsonPropertyName("recipe")]
    [JsonProperty("recipe")]
    public required Recipe Recipe { get; init; }

    /// <summary>
    /// Provenance information about where the recipe was sourced from.
    /// </summary>
    [JsonPropertyName("source")]
    [JsonProperty("source")]
    public required RecipeSource Source { get; init; }

    /// <summary>
    /// Validation results for the extracted recipe.
    /// </summary>
    [JsonPropertyName("validationReport")]
    [JsonProperty("validationReport")]
    public required ValidationReport ValidationReport { get; init; }

    /// <summary>
    /// Optional similarity analysis report for verbatim content checks.
    /// </summary>
    [JsonPropertyName("similarityReport")]
    [JsonProperty("similarityReport")]
    public SimilarityReport? SimilarityReport { get; init; }

    /// <summary>
    /// References to artifacts stored during the ingest process.
    /// </summary>
    [JsonPropertyName("artifacts")]
    [JsonProperty("artifacts")]
    public List<ArtifactRef> Artifacts { get; init; } = [];
}
