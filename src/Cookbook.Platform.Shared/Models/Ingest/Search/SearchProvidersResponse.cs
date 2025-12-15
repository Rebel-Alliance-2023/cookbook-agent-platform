using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest.Search;

/// <summary>
/// Response containing available search providers.
/// </summary>
public record SearchProvidersResponse
{
    /// <summary>
    /// The default provider ID to use when none is specified.
    /// </summary>
    [JsonPropertyName("defaultProviderId")]
    [JsonProperty("defaultProviderId")]
    public required string DefaultProviderId { get; init; }

    /// <summary>
    /// List of available search providers.
    /// </summary>
    [JsonPropertyName("providers")]
    [JsonProperty("providers")]
    public IReadOnlyList<SearchProviderDescriptor> Providers { get; init; } = [];
}
