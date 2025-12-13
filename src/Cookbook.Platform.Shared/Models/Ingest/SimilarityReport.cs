using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Represents the similarity analysis results comparing extracted content against the source.
/// Used for verbatim content guardrails.
/// </summary>
public record SimilarityReport
{
    /// <summary>
    /// The maximum number of contiguous tokens that overlap between source and extracted content.
    /// </summary>
    [JsonPropertyName("maxContiguousTokenOverlap")]
    [JsonProperty("maxContiguousTokenOverlap")]
    public int MaxContiguousTokenOverlap { get; init; }

    /// <summary>
    /// The maximum n-gram (5-gram) Jaccard similarity score between source and extracted content.
    /// Value between 0.0 and 1.0.
    /// </summary>
    [JsonPropertyName("maxNgramSimilarity")]
    [JsonProperty("maxNgramSimilarity")]
    public double MaxNgramSimilarity { get; init; }

    /// <summary>
    /// Indicates whether the similarity levels violate the configured policy thresholds.
    /// </summary>
    [JsonPropertyName("violatesPolicy")]
    [JsonProperty("violatesPolicy")]
    public bool ViolatesPolicy { get; init; }

    /// <summary>
    /// Additional details about the similarity analysis, such as which sections had high overlap.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonProperty("details")]
    public string? Details { get; init; }
}
