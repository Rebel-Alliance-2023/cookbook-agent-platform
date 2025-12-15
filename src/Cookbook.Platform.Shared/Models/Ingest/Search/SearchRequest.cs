using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest.Search;

/// <summary>
/// Represents a request to search for recipe URLs using a search provider.
/// </summary>
public record SearchRequest
{
    /// <summary>
    /// The search query string (e.g., "chocolate chip cookie recipe").
    /// </summary>
    [JsonPropertyName("query")]
    [JsonProperty("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Maximum number of results to return. Defaults to 10.
    /// </summary>
    [JsonPropertyName("maxResults")]
    [JsonProperty("maxResults")]
    public int MaxResults { get; init; } = 10;

    /// <summary>
    /// Market/locale for the search (e.g., "en-US", "en-GB").
    /// </summary>
    [JsonPropertyName("market")]
    [JsonProperty("market")]
    public string? Market { get; init; }

    /// <summary>
    /// Locale for the search results (e.g., "en", "fr").
    /// </summary>
    [JsonPropertyName("locale")]
    [JsonProperty("locale")]
    public string? Locale { get; init; }

    /// <summary>
    /// Safe search filtering level. Defaults to "moderate".
    /// Valid values: "off", "moderate", "strict".
    /// </summary>
    [JsonPropertyName("safeSearch")]
    [JsonProperty("safeSearch")]
    public string SafeSearch { get; init; } = "moderate";
}
