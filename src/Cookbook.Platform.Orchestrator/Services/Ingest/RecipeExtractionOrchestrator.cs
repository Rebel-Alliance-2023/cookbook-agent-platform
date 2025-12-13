using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Orchestrates recipe extraction, trying JSON-LD first then falling back to LLM.
/// </summary>
public class RecipeExtractionOrchestrator
{
    private readonly JsonLdRecipeExtractor _jsonLdExtractor;
    private readonly LlmRecipeExtractor _llmExtractor;
    private readonly ILogger<RecipeExtractionOrchestrator> _logger;

    public RecipeExtractionOrchestrator(
        JsonLdRecipeExtractor jsonLdExtractor,
        LlmRecipeExtractor llmExtractor,
        ILogger<RecipeExtractionOrchestrator> logger)
    {
        _jsonLdExtractor = jsonLdExtractor;
        _llmExtractor = llmExtractor;
        _logger = logger;
    }

    /// <summary>
    /// Extracts a recipe from sanitized content, trying JSON-LD first then falling back to LLM.
    /// </summary>
    /// <param name="sanitizedContent">The sanitized HTML content.</param>
    /// <param name="context">Extraction context with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extraction result with recipe and source information.</returns>
    public async Task<RecipeExtractionResult> ExtractAsync(
        SanitizedContent sanitizedContent,
        ExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting recipe extraction for {Url}", context.SourceUrl);

        ExtractionResult? result = null;

        // Try JSON-LD extraction first if we have structured data
        if (sanitizedContent.HasRecipeJsonLd && !string.IsNullOrEmpty(sanitizedContent.RecipeJsonLd))
        {
            _logger.LogDebug("Attempting JSON-LD extraction");
            result = await _jsonLdExtractor.ExtractAsync(
                sanitizedContent.RecipeJsonLd,
                context,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("JSON-LD extraction successful for '{Name}'", result.Recipe?.Name);
                return CreateResult(result, sanitizedContent, context);
            }

            _logger.LogWarning("JSON-LD extraction failed: {Error}. Falling back to LLM.", result.Error);
        }
        else
        {
            _logger.LogDebug("No Recipe JSON-LD found, using LLM extraction");
        }

        // Fall back to LLM extraction
        _logger.LogDebug("Attempting LLM extraction on text content ({Length} chars)", 
            sanitizedContent.TextContent.Length);
        
        result = await _llmExtractor.ExtractAsync(
            sanitizedContent.TextContent,
            context,
            cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("LLM extraction successful for '{Name}' (repairs: {Repairs})", 
                result.Recipe?.Name, result.RepairAttempts);
        }
        else
        {
            _logger.LogWarning("LLM extraction failed: {Error}", result.Error);
        }

        return CreateResult(result, sanitizedContent, context);
    }

    /// <summary>
    /// Creates the final extraction result with source information.
    /// </summary>
    private RecipeExtractionResult CreateResult(
        ExtractionResult extractionResult,
        SanitizedContent sanitizedContent,
        ExtractionContext context)
    {
        if (!extractionResult.Success || extractionResult.Recipe == null)
        {
            return new RecipeExtractionResult
            {
                Success = false,
                Error = extractionResult.Error,
                ErrorCode = extractionResult.ErrorCode,
                Method = extractionResult.Method,
                Warnings = extractionResult.Warnings
            };
        }

        // Build RecipeSource with extraction method
        var source = new RecipeSource
        {
            Url = context.SourceUrl ?? "",
            UrlHash = ComputeUrlHash(context.SourceUrl ?? ""),
            SiteName = sanitizedContent.Metadata.SiteName,
            Author = sanitizedContent.Metadata.Author,
            RetrievedAt = DateTime.UtcNow,
            ExtractionMethod = extractionResult.Method.ToString()
        };

        return new RecipeExtractionResult
        {
            Success = true,
            Recipe = extractionResult.Recipe,
            Source = source,
            Method = extractionResult.Method,
            Confidence = extractionResult.Confidence,
            Warnings = extractionResult.Warnings,
            RawJsonLd = extractionResult.Method == ExtractionMethod.JsonLd 
                ? sanitizedContent.RecipeJsonLd 
                : null,
            RepairAttempts = extractionResult.RepairAttempts
        };
    }

    /// <summary>
    /// Computes a URL hash for duplicate detection.
    /// </summary>
    private static string ComputeUrlHash(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(url.ToLowerInvariant());
        var hash = sha256.ComputeHash(bytes);
        
        // Base64url encode and take first 22 chars
        var base64 = Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        return base64.Length > 22 ? base64[..22] : base64;
    }
}

/// <summary>
/// Result of the recipe extraction orchestration.
/// </summary>
public record RecipeExtractionResult
{
    /// <summary>
    /// Whether extraction was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The extracted recipe (if successful).
    /// </summary>
    public Recipe? Recipe { get; init; }

    /// <summary>
    /// Source/provenance information.
    /// </summary>
    public RecipeSource? Source { get; init; }

    /// <summary>
    /// The extraction method used.
    /// </summary>
    public ExtractionMethod Method { get; init; }

    /// <summary>
    /// Confidence level of the extraction.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Error code if extraction failed.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Warnings generated during extraction.
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// The raw JSON-LD if JSON-LD extraction was used.
    /// </summary>
    public string? RawJsonLd { get; init; }

    /// <summary>
    /// Number of repair attempts for LLM extraction.
    /// </summary>
    public int RepairAttempts { get; init; }
}
