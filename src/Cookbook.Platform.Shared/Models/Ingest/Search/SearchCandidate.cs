using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest.Search;

/// <summary>
/// Represents a search result candidate returned from a search provider.
/// </summary>
public record SearchCandidate
{
    /// <summary>
    /// The URL of the search result.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public required string Url { get; init; }

    /// <summary>
    /// The title of the search result.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public required string Title { get; init; }

    /// <summary>
    /// A snippet or description of the search result content.
    /// </summary>
    [JsonPropertyName("snippet")]
    [JsonProperty("snippet")]
    public string? Snippet { get; init; }

    /// <summary>
    /// The name of the website hosting the result.
    /// </summary>
    [JsonPropertyName("siteName")]
    [JsonProperty("siteName")]
    public string? SiteName { get; init; }

    /// <summary>
    /// A relevance score assigned by the search provider (higher is more relevant).
    /// May be null if the provider does not supply a score.
    /// </summary>
    [JsonPropertyName("score")]
    [JsonProperty("score")]
    public double? Score { get; init; }

    /// <summary>
    /// The position of this result in the original search results (0-based index).
    /// </summary>
    [JsonPropertyName("position")]
    [JsonProperty("position")]
    public int Position { get; init; }
}
