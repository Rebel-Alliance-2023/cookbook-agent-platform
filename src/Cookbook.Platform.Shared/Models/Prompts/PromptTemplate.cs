using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Prompts;

/// <summary>
/// Represents a versioned prompt template stored in the prompt registry.
/// Templates are rendered using Scriban syntax with variable substitution.
/// </summary>
public record PromptTemplate
{
    /// <summary>
    /// Unique identifier for the template (e.g., "ingest.extract.v1").
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name for the template (e.g., "Ingest Extract").
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The phase this prompt is used for (e.g., "Ingest.Extract", "Ingest.Normalize").
    /// This is the partition key for the prompts container.
    /// </summary>
    [JsonPropertyName("phase")]
    [JsonProperty("phase")]
    public required string Phase { get; init; }

    /// <summary>
    /// Version number of the template for tracking changes.
    /// </summary>
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Indicates whether this template is the active version for its phase.
    /// Only one template per phase should be active at a time.
    /// </summary>
    [JsonPropertyName("isActive")]
    [JsonProperty("isActive")]
    public bool IsActive { get; init; }

    /// <summary>
    /// The system prompt text sent to the LLM.
    /// </summary>
    [JsonPropertyName("systemPrompt")]
    [JsonProperty("systemPrompt")]
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// The user prompt template containing Scriban variables (e.g., {{ url }}, {{ content }}).
    /// </summary>
    [JsonPropertyName("userPromptTemplate")]
    [JsonProperty("userPromptTemplate")]
    public required string UserPromptTemplate { get; init; }

    /// <summary>
    /// Additional constraints or metadata for the template.
    /// </summary>
    [JsonPropertyName("constraints")]
    [JsonProperty("constraints")]
    public Dictionary<string, string> Constraints { get; init; } = new();

    /// <summary>
    /// List of required variable names that must be provided during rendering.
    /// Missing required variables will cause a PromptRenderException.
    /// </summary>
    [JsonPropertyName("requiredVariables")]
    [JsonProperty("requiredVariables")]
    public List<string> RequiredVariables { get; init; } = [];

    /// <summary>
    /// List of optional variable names that can be provided during rendering.
    /// Missing optional variables render as empty strings.
    /// </summary>
    [JsonPropertyName("optionalVariables")]
    [JsonProperty("optionalVariables")]
    public List<string> OptionalVariables { get; init; } = [];

    /// <summary>
    /// Maximum token budget for the rendered prompt (for truncation logic).
    /// </summary>
    [JsonPropertyName("maxTokens")]
    [JsonProperty("maxTokens")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Timestamp when the template was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the template was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Identity of the user who created the template.
    /// </summary>
    [JsonPropertyName("createdBy")]
    [JsonProperty("createdBy")]
    public string? CreatedBy { get; init; }
}
