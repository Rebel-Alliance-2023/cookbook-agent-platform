using System.Text;
using System.Text.Json;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Azure Blob Storage implementation of IArtifactStorageService.
/// </summary>
public class BlobArtifactStorageService : IArtifactStorageService
{
    private readonly IBlobStorage _blobStorage;
    private readonly ILogger<BlobArtifactStorageService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BlobArtifactStorageService(IBlobStorage blobStorage, ILogger<BlobArtifactStorageService> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }

    #region Specialized Storage Methods

    /// <summary>
    /// Stores raw HTML content fetched from a URL.
    /// Path: {threadId}/{taskId}/fetch/raw.html
    /// </summary>
    public async Task<ArtifactRef> StoreRawHtmlAsync(string threadId, string taskId, string content, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing raw HTML for task {TaskId} ({Length} chars)", taskId, content.Length);
        
        return await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.RawHtml,
            content,
            "text/html",
            "fetch",
            cancellationToken);
    }

    /// <summary>
    /// Stores sanitized text content after HTML processing.
    /// Path: {threadId}/{taskId}/sanitize/sanitized.txt
    /// Also stores page metadata as separate artifact.
    /// </summary>
    public async Task<ArtifactRef> StoreSanitizedContentAsync(string threadId, string taskId, SanitizedContent content, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing sanitized content for task {TaskId} ({Length} chars)", taskId, content.TextContent.Length);

        // Store the text content
        var textRef = await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.SanitizedText,
            content.TextContent,
            "text/plain",
            "sanitize",
            cancellationToken);

        // Store page metadata as a separate artifact
        var metadataJson = JsonSerializer.Serialize(content.Metadata, JsonOptions);
        await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.PageMetadata,
            metadataJson,
            "application/json",
            "sanitize",
            cancellationToken);

        return textRef;
    }

    /// <summary>
    /// Stores extracted JSON-LD structured data.
    /// Path: {threadId}/{taskId}/extract/recipe.jsonld
    /// </summary>
    public async Task<ArtifactRef> StoreJsonLdAsync(string threadId, string taskId, string jsonLd, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing JSON-LD for task {TaskId} ({Length} chars)", taskId, jsonLd.Length);

        return await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.JsonLd,
            jsonLd,
            "application/ld+json",
            "extract",
            cancellationToken);
    }

    /// <summary>
    /// Stores the recipe extraction result.
    /// Path: {threadId}/{taskId}/extract/extraction.json
    /// Also stores the recipe separately.
    /// </summary>
    public async Task<ArtifactRef> StoreExtractionResultAsync(string threadId, string taskId, RecipeExtractionResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing extraction result for task {TaskId} (method: {Method})", taskId, result.Method);

        // Store the full extraction result
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);
        var resultRef = await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.ExtractionResult,
            resultJson,
            "application/json",
            "extract",
            cancellationToken);

        // Store the recipe separately if successful
        if (result.Success && result.Recipe != null)
        {
            var recipeJson = JsonSerializer.Serialize(result.Recipe, JsonOptions);
            await StoreAsync(
                threadId,
                taskId,
                ArtifactTypes.Recipe,
                recipeJson,
                "application/json",
                "extract",
                cancellationToken);
        }

        return resultRef;
    }

    /// <summary>
    /// Stores the validation report.
    /// Path: {threadId}/{taskId}/validate/validation.json
    /// </summary>
    public async Task<ArtifactRef> StoreValidationReportAsync(string threadId, string taskId, ValidationReport report, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing validation report for task {TaskId} (valid: {IsValid})", taskId, report.IsValid);

        var json = JsonSerializer.Serialize(report, JsonOptions);
        return await StoreAsync(
            threadId,
            taskId,
            ArtifactTypes.ValidationReport,
            json,
            "application/json",
            "validate",
            cancellationToken);
    }

    #endregion

    #region Generic Storage Methods

    /// <summary>
    /// Stores a generic artifact with specified type and content.
    /// </summary>
    public Task<ArtifactRef> StoreAsync(string threadId, string taskId, string artifactType, string content, string contentType, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return StoreAsync(threadId, taskId, artifactType, bytes, contentType, cancellationToken);
    }

    /// <summary>
    /// Stores a generic artifact with specified type and binary content.
    /// </summary>
    public async Task<ArtifactRef> StoreAsync(string threadId, string taskId, string artifactType, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(threadId, taskId, artifactType);
        
        _logger.LogDebug("Storing artifact at {Path} ({Size} bytes)", path, content.Length);

        var uri = await _blobStorage.UploadAsync(path, content, contentType, cancellationToken);

        return new ArtifactRef
        {
            Type = artifactType,
            Uri = uri
        };
    }

    /// <summary>
    /// Internal method to store with explicit phase.
    /// </summary>
    private async Task<ArtifactRef> StoreAsync(string threadId, string taskId, string artifactType, string content, string contentType, string phase, CancellationToken cancellationToken)
    {
        var path = BuildPath(threadId, taskId, phase, artifactType);
        var bytes = Encoding.UTF8.GetBytes(content);

        _logger.LogDebug("Storing artifact at {Path} ({Size} bytes)", path, bytes.Length);

        var uri = await _blobStorage.UploadAsync(path, bytes, contentType, cancellationToken);

        return new ArtifactRef
        {
            Type = artifactType,
            Uri = uri
        };
    }

    #endregion

    #region Retrieval Methods

    /// <summary>
    /// Retrieves an artifact by its path as string.
    /// </summary>
    public async Task<string?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await _blobStorage.DownloadAsync(path, cancellationToken);
        return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
    }

    /// <summary>
    /// Retrieves an artifact as bytes by its path.
    /// </summary>
    public Task<byte[]?> GetBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return _blobStorage.DownloadAsync(path, cancellationToken);
    }

    /// <summary>
    /// Lists all artifacts for a task.
    /// </summary>
    public async Task<List<ArtifactRef>> ListAsync(string threadId, string taskId, CancellationToken cancellationToken = default)
    {
        var prefix = $"{threadId}/{taskId}/";
        var blobs = await _blobStorage.ListAsync(prefix, cancellationToken);

        return blobs.Select(path => new ArtifactRef
        {
            Type = ExtractArtifactType(path),
            Uri = path
        }).ToList();
    }

    /// <summary>
    /// Checks if an artifact exists.
    /// </summary>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return _blobStorage.ExistsAsync(path, cancellationToken);
    }

    /// <summary>
    /// Deletes an artifact.
    /// </summary>
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        return _blobStorage.DeleteAsync(path, cancellationToken);
    }

    #endregion

    #region Path Building

    /// <summary>
    /// Builds the blob path for an artifact.
    /// Format: {threadId}/{taskId}/{artifactType}
    /// </summary>
    private static string BuildPath(string threadId, string taskId, string artifactType)
    {
        return $"{SanitizePathSegment(threadId)}/{SanitizePathSegment(taskId)}/{artifactType}";
    }

    /// <summary>
    /// Builds the blob path for an artifact with phase.
    /// Format: {threadId}/{taskId}/{phase}/{artifactType}
    /// </summary>
    private static string BuildPath(string threadId, string taskId, string phase, string artifactType)
    {
        return $"{SanitizePathSegment(threadId)}/{SanitizePathSegment(taskId)}/{phase}/{artifactType}";
    }

    /// <summary>
    /// Sanitizes a path segment to ensure it's safe for blob storage.
    /// </summary>
    private static string SanitizePathSegment(string segment)
    {
        // Replace any characters that might cause issues in blob paths
        return segment
            .Replace('\\', '-')
            .Replace(':', '-')
            .Replace('*', '-')
            .Replace('?', '-')
            .Replace('"', '-')
            .Replace('<', '-')
            .Replace('>', '-')
            .Replace('|', '-');
    }

    /// <summary>
    /// Extracts the artifact type from a full blob path.
    /// </summary>
    private static string ExtractArtifactType(string path)
    {
        // Path format: {threadId}/{taskId}/{phase}/{artifactType} or {threadId}/{taskId}/{artifactType}
        var segments = path.Split('/');
        return segments.Length > 0 ? segments[^1] : path;
    }

    #endregion
}
