using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Service for storing and retrieving ingest artifacts.
/// </summary>
public interface IArtifactStorageService
{
    /// <summary>
    /// Stores raw HTML content fetched from a URL.
    /// </summary>
    Task<ArtifactRef> StoreRawHtmlAsync(string threadId, string taskId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores sanitized text content after HTML processing.
    /// </summary>
    Task<ArtifactRef> StoreSanitizedContentAsync(string threadId, string taskId, SanitizedContent content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores extracted JSON-LD structured data.
    /// </summary>
    Task<ArtifactRef> StoreJsonLdAsync(string threadId, string taskId, string jsonLd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the recipe extraction result.
    /// </summary>
    Task<ArtifactRef> StoreExtractionResultAsync(string threadId, string taskId, RecipeExtractionResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the validation report.
    /// </summary>
    Task<ArtifactRef> StoreValidationReportAsync(string threadId, string taskId, ValidationReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a generic artifact with specified type and content.
    /// </summary>
    Task<ArtifactRef> StoreAsync(string threadId, string taskId, string artifactType, string content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a generic artifact with specified type and binary content.
    /// </summary>
    Task<ArtifactRef> StoreAsync(string threadId, string taskId, string artifactType, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an artifact by its path.
    /// </summary>
    Task<string?> GetAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an artifact as bytes by its path.
    /// </summary>
    Task<byte[]?> GetBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all artifacts for a task.
    /// </summary>
    Task<List<ArtifactRef>> ListAsync(string threadId, string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an artifact exists.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an artifact.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Known artifact types for the ingest process.
/// </summary>
public static class ArtifactTypes
{
    /// <summary>
    /// Raw HTML content fetched from URL.
    /// </summary>
    public const string RawHtml = "raw.html";

    /// <summary>
    /// Sanitized text content after HTML processing.
    /// </summary>
    public const string SanitizedText = "sanitized.txt";

    /// <summary>
    /// Page metadata extracted during sanitization.
    /// </summary>
    public const string PageMetadata = "page.meta.json";

    /// <summary>
    /// Extracted JSON-LD structured data.
    /// </summary>
    public const string JsonLd = "recipe.jsonld";

    /// <summary>
    /// Recipe extraction result.
    /// </summary>
    public const string ExtractionResult = "extraction.json";

    /// <summary>
    /// Extracted recipe in canonical format.
    /// </summary>
    public const string Recipe = "recipe.json";

    /// <summary>
    /// Validation report.
    /// </summary>
    public const string ValidationReport = "validation.json";

    /// <summary>
    /// LLM repair attempt artifact.
    /// </summary>
    public const string RepairAttempt = "repair.{0}.json";

    /// <summary>
    /// Final recipe draft.
    /// </summary>
    public const string Draft = "draft.json";
}
