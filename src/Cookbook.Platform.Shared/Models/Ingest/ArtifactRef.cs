using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Represents a reference to an artifact stored during the ingest process.
/// </summary>
public record ArtifactRef
{
    /// <summary>
    /// The type of artifact (e.g., "snapshot.txt", "page.meta.json", "recipe.jsonld", "draft.recipe.json").
    /// </summary>
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public required string Type { get; init; }

    /// <summary>
    /// The URI where the artifact is stored.
    /// </summary>
    [JsonPropertyName("uri")]
    [JsonProperty("uri")]
    public required string Uri { get; init; }
}
