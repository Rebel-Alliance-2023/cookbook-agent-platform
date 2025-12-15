using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Ingest mode for recipe import tasks.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum IngestMode
{
    /// <summary>
    /// Import recipe from a specific URL.
    /// </summary>
    Url,
    
    /// <summary>
    /// Discover recipes by search query.
    /// </summary>
    Query,
    
    /// <summary>
    /// Normalize an existing stored recipe.
    /// </summary>
    Normalize
}

/// <summary>
/// Prompt selection for ingest phases.
/// </summary>
public record PromptSelection
{
    /// <summary>
    /// Optional prompt ID for discovery phase.
    /// </summary>
    [JsonPropertyName("discoverPromptId")]
    [JsonProperty("discoverPromptId")]
    public string? DiscoverPromptId { get; init; }
    
    /// <summary>
    /// Optional prompt ID for extraction phase.
    /// </summary>
    [JsonPropertyName("extractPromptId")]
    [JsonProperty("extractPromptId")]
    public string? ExtractPromptId { get; init; }
    
    /// <summary>
    /// Optional prompt ID for normalization phase.
    /// </summary>
    [JsonPropertyName("normalizePromptId")]
    [JsonProperty("normalizePromptId")]
    public string? NormalizePromptId { get; init; }
}

/// <summary>
/// Prompt overrides for ingest phases (ad-hoc prompts for this task only).
/// </summary>
public record PromptOverrides
{
    /// <summary>
    /// Override prompt text for discovery phase.
    /// </summary>
    [JsonPropertyName("discoverOverride")]
    [JsonProperty("discoverOverride")]
    public string? DiscoverOverride { get; init; }
    
    /// <summary>
    /// Override prompt text for extraction phase.
    /// </summary>
    [JsonPropertyName("extractOverride")]
    [JsonProperty("extractOverride")]
    public string? ExtractOverride { get; init; }
    
    /// <summary>
    /// Override prompt text for normalization phase.
    /// </summary>
    [JsonPropertyName("normalizeOverride")]
    [JsonProperty("normalizeOverride")]
    public string? NormalizeOverride { get; init; }
}

/// <summary>
/// Search constraints for query-based discovery.
/// </summary>
public record IngestConstraints
{
    /// <summary>
    /// Filter by diet type (e.g., "vegetarian", "vegan", "keto").
    /// </summary>
    [JsonPropertyName("dietType")]
    [JsonProperty("dietType")]
    public string? DietType { get; init; }
    
    /// <summary>
    /// Filter by cuisine type (e.g., "Japanese", "Italian").
    /// </summary>
    [JsonPropertyName("cuisine")]
    [JsonProperty("cuisine")]
    public string? Cuisine { get; init; }
    
    /// <summary>
    /// Maximum prep time in minutes.
    /// </summary>
    [JsonPropertyName("maxPrepMinutes")]
    [JsonProperty("maxPrepMinutes")]
    public int? MaxPrepMinutes { get; init; }
}

/// <summary>
/// Search provider settings for query-based discovery.
/// </summary>
public record SearchSettings
{
    /// <summary>
    /// The ID of the search provider to use (e.g., "brave", "google").
    /// If not specified, the default provider will be used.
    /// </summary>
    [JsonPropertyName("providerId")]
    [JsonProperty("providerId")]
    public string? ProviderId { get; init; }

    /// <summary>
    /// Maximum number of candidate URLs to return from search.
    /// </summary>
    [JsonPropertyName("maxResults")]
    [JsonProperty("maxResults")]
    public int? MaxResults { get; init; }

    /// <summary>
    /// Market/locale for search results (e.g., "en-US").
    /// </summary>
    [JsonPropertyName("market")]
    [JsonProperty("market")]
    public string? Market { get; init; }

    /// <summary>
    /// Safe search filtering level (e.g., "off", "moderate", "strict").
    /// </summary>
    [JsonPropertyName("safeSearch")]
    [JsonProperty("safeSearch")]
    public string? SafeSearch { get; init; }
}

/// <summary>
/// Options for normalize mode.
/// </summary>
public record NormalizeOptions
{
    /// <summary>
    /// Specific areas to focus on for normalization.
    /// </summary>
    [JsonPropertyName("focusAreas")]
    [JsonProperty("focusAreas")]
    public IReadOnlyList<string>? FocusAreas { get; init; }

    /// <summary>
    /// Whether to automatically apply low-risk changes.
    /// </summary>
    [JsonPropertyName("autoApplyLowRisk")]
    [JsonProperty("autoApplyLowRisk")]
    public bool AutoApplyLowRisk { get; init; }

    /// <summary>
    /// Maximum risk level to include in suggestions.
    /// </summary>
    [JsonPropertyName("maxRiskLevel")]
    [JsonProperty("maxRiskLevel")]
    public NormalizePatchRiskCategory MaxRiskLevel { get; init; } = NormalizePatchRiskCategory.High;
}

/// <summary>
/// Payload for ingest agent tasks.
/// </summary>
public record IngestPayload
{
    /// <summary>
    /// The ingest mode (Url, Query, or Normalize).
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonProperty("mode")]
    public required IngestMode Mode { get; init; }
    
    /// <summary>
    /// URL to import recipe from (required when Mode=Url).
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public string? Url { get; init; }
    
    /// <summary>
    /// Search query for discovery (required when Mode=Query).
    /// </summary>
    [JsonPropertyName("query")]
    [JsonProperty("query")]
    public string? Query { get; init; }
    
    /// <summary>
    /// Existing recipe ID to normalize (required when Mode=Normalize).
    /// </summary>
    [JsonPropertyName("recipeId")]
    [JsonProperty("recipeId")]
    public string? RecipeId { get; init; }
    
    /// <summary>
    /// Options for normalize mode.
    /// </summary>
    [JsonPropertyName("normalizeOptions")]
    [JsonProperty("normalizeOptions")]
    public NormalizeOptions? NormalizeOptions { get; init; }
    
    /// <summary>
    /// Search provider settings for query-based discovery (Mode=Query).
    /// </summary>
    [JsonPropertyName("search")]
    [JsonProperty("search")]
    public SearchSettings? Search { get; init; }
    
    /// <summary>
    /// Search constraints for query-based discovery.
    /// </summary>
    [JsonPropertyName("constraints")]
    [JsonProperty("constraints")]
    public IngestConstraints? Constraints { get; init; }
    
    /// <summary>
    /// Prompt IDs to use for each phase (null = use active prompt).
    /// </summary>
    [JsonPropertyName("promptSelection")]
    [JsonProperty("promptSelection")]
    public PromptSelection? PromptSelection { get; init; }
    
    /// <summary>
    /// Ad-hoc prompt overrides for this task only.
    /// </summary>
    [JsonPropertyName("promptOverrides")]
    [JsonProperty("promptOverrides")]
    public PromptOverrides? PromptOverrides { get; init; }
}
