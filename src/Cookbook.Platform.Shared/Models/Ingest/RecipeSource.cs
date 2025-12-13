using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Represents the provenance information for an imported recipe.
/// </summary>
public record RecipeSource
{
    /// <summary>
    /// The original URL from which the recipe was retrieved.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public required string Url { get; init; }

    /// <summary>
    /// Base64url-encoded SHA256 hash (first 22 chars) of the normalized URL for duplicate detection.
    /// </summary>
    [JsonPropertyName("urlHash")]
    [JsonProperty("urlHash")]
    public required string UrlHash { get; init; }

    /// <summary>
    /// The name of the website from which the recipe was retrieved.
    /// </summary>
    [JsonPropertyName("siteName")]
    [JsonProperty("siteName")]
    public string? SiteName { get; init; }

    /// <summary>
    /// The author of the recipe, if available.
    /// </summary>
    [JsonPropertyName("author")]
    [JsonProperty("author")]
    public string? Author { get; init; }

    /// <summary>
    /// The timestamp when the recipe was retrieved from the source.
    /// </summary>
    [JsonPropertyName("retrievedAt")]
    [JsonProperty("retrievedAt")]
    public DateTime RetrievedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The method used to extract the recipe (e.g., "JsonLd", "Llm").
    /// </summary>
    [JsonPropertyName("extractionMethod")]
    [JsonProperty("extractionMethod")]
    public string? ExtractionMethod { get; init; }

    /// <summary>
    /// Hint about the license or copyright of the source content.
    /// </summary>
    [JsonPropertyName("licenseHint")]
    [JsonProperty("licenseHint")]
    public string? LicenseHint { get; init; }
}
