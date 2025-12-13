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
