using System.Text.Json.Serialization;
using Cookbook.Platform.Shared.Models;
using Newtonsoft.Json;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// The method used to extract a recipe from content.
/// </summary>
public enum ExtractionMethod
{
    /// <summary>
    /// Extracted from Schema.org JSON-LD structured data.
    /// </summary>
    JsonLd,
    
    /// <summary>
    /// Extracted using LLM-based content analysis.
    /// </summary>
    Llm,
    
    /// <summary>
    /// Manual entry or unknown method.
    /// </summary>
    Manual
}

/// <summary>
/// Result of a recipe extraction attempt.
/// </summary>
public record ExtractionResult
{
    /// <summary>
    /// Whether the extraction was successful.
    /// </summary>
    [JsonPropertyName("success")]
    [JsonProperty("success")]
    public required bool Success { get; init; }
    
    /// <summary>
    /// The extracted recipe (if successful).
    /// </summary>
    [JsonPropertyName("recipe")]
    [JsonProperty("recipe")]
    public Recipe? Recipe { get; init; }
    
    /// <summary>
    /// The method used to extract the recipe.
    /// </summary>
    [JsonPropertyName("method")]
    [JsonProperty("method")]
    public ExtractionMethod Method { get; init; }
    
    /// <summary>
    /// Confidence level of the extraction (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    [JsonProperty("confidence")]
    public double Confidence { get; init; }
    
    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonProperty("error")]
    public string? Error { get; init; }
    
    /// <summary>
    /// Error code for structured error handling.
    /// </summary>
    [JsonPropertyName("errorCode")]
    [JsonProperty("errorCode")]
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// Warnings generated during extraction.
    /// </summary>
    [JsonPropertyName("warnings")]
    [JsonProperty("warnings")]
    public List<string> Warnings { get; init; } = [];
    
    /// <summary>
    /// The raw source data used for extraction (for debugging/artifacts).
    /// </summary>
    [JsonPropertyName("rawSource")]
    [JsonProperty("rawSource")]
    public string? RawSource { get; init; }
    
    /// <summary>
    /// Number of repair attempts made (for LLM extraction).
    /// </summary>
    [JsonPropertyName("repairAttempts")]
    [JsonProperty("repairAttempts")]
    public int RepairAttempts { get; init; }

    /// <summary>
    /// Creates a successful extraction result.
    /// </summary>
    public static ExtractionResult Succeeded(Recipe recipe, ExtractionMethod method, double confidence = 1.0, string? rawSource = null) => new()
    {
        Success = true,
        Recipe = recipe,
        Method = method,
        Confidence = confidence,
        RawSource = rawSource
    };

    /// <summary>
    /// Creates a failed extraction result.
    /// </summary>
    public static ExtractionResult Failed(string error, string errorCode, ExtractionMethod method) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode,
        Method = method
    };
}

/// <summary>
/// Interface for extracting recipes from content.
/// </summary>
public interface IRecipeExtractor
{
    /// <summary>
    /// The extraction method this extractor uses.
    /// </summary>
    ExtractionMethod Method { get; }
    
    /// <summary>
    /// Attempts to extract a recipe from the provided content.
    /// </summary>
    /// <param name="content">The content to extract from.</param>
    /// <param name="context">Additional context for extraction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extraction result.</returns>
    Task<ExtractionResult> ExtractAsync(string content, ExtractionContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if this extractor can handle the provided content.
    /// </summary>
    /// <param name="content">The content to check.</param>
    /// <returns>True if this extractor can attempt extraction.</returns>
    bool CanExtract(string content);
}

/// <summary>
/// Context information for recipe extraction.
/// </summary>
public record ExtractionContext
{
    /// <summary>
    /// The source URL of the content.
    /// </summary>
    public string? SourceUrl { get; init; }
    
    /// <summary>
    /// Page metadata from sanitization.
    /// </summary>
    public PageMetadata? Metadata { get; init; }
    
    /// <summary>
    /// The task ID for artifact storage.
    /// </summary>
    public string? TaskId { get; init; }
    
    /// <summary>
    /// Maximum character budget for LLM extraction.
    /// </summary>
    public int ContentBudget { get; init; } = 60_000;
    
    /// <summary>
    /// Optional prompt override for LLM extraction.
    /// </summary>
    public string? PromptOverride { get; init; }
}
