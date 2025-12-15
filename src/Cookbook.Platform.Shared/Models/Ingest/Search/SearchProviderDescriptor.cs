using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest.Search;

/// <summary>
/// Describes a search provider's capabilities and status.
/// </summary>
public record SearchProviderDescriptor
{
    /// <summary>
    /// The unique identifier for the search provider.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The human-readable display name of the provider.
    /// </summary>
    [JsonPropertyName("displayName")]
    [JsonProperty("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether this provider is currently enabled and available.
    /// </summary>
    [JsonPropertyName("enabled")]
    [JsonProperty("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether this is the default search provider.
    /// </summary>
    [JsonPropertyName("isDefault")]
    [JsonProperty("isDefault")]
    public bool IsDefault { get; init; }

    /// <summary>
    /// The capabilities supported by this provider.
    /// </summary>
    [JsonPropertyName("capabilities")]
    [JsonProperty("capabilities")]
    public SearchProviderCapabilities Capabilities { get; init; } = new();
}

/// <summary>
/// Describes the capabilities of a search provider.
/// </summary>
public record SearchProviderCapabilities
{
    /// <summary>
    /// Whether the provider supports market/locale filtering.
    /// </summary>
    [JsonPropertyName("supportsMarket")]
    [JsonProperty("supportsMarket")]
    public bool SupportsMarket { get; init; } = true;

    /// <summary>
    /// Whether the provider supports safe search filtering.
    /// </summary>
    [JsonPropertyName("supportsSafeSearch")]
    [JsonProperty("supportsSafeSearch")]
    public bool SupportsSafeSearch { get; init; } = true;

    /// <summary>
    /// Whether the provider supports site restrictions.
    /// </summary>
    [JsonPropertyName("supportsSiteRestrictions")]
    [JsonProperty("supportsSiteRestrictions")]
    public bool SupportsSiteRestrictions { get; init; } = false;

    /// <summary>
    /// Maximum number of results the provider can return per request.
    /// </summary>
    [JsonPropertyName("maxResultsPerRequest")]
    [JsonProperty("maxResultsPerRequest")]
    public int MaxResultsPerRequest { get; init; } = 10;

    /// <summary>
    /// Rate limit (requests per minute) for this provider.
    /// </summary>
    [JsonPropertyName("rateLimitPerMinute")]
    [JsonProperty("rateLimitPerMinute")]
    public int RateLimitPerMinute { get; init; }
}
