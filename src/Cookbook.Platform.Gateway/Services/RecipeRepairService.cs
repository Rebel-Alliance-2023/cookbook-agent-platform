using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage;
using Microsoft.Extensions.Logging;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Services;

/// <summary>
/// Service for manually triggering repair/paraphrase on a recipe draft with high similarity.
/// </summary>
public interface IRecipeRepairService
{
    /// <summary>
    /// Attempts to repair a recipe draft by paraphrasing high-similarity content.
    /// </summary>
    Task<RecipeRepairResult> RepairAsync(string taskId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a repair operation.
/// </summary>
public record RecipeRepairResult
{
    public bool Success { get; init; }
    public RecipeDraft? RepairedDraft { get; init; }
    public SimilarityReport? NewSimilarityReport { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public int StatusCode { get; init; }
    public bool StillViolatesPolicy { get; init; }

    public static RecipeRepairResult Ok(RecipeDraft draft, SimilarityReport report, bool stillViolates) =>
        new()
        {
            Success = true,
            RepairedDraft = draft,
            NewSimilarityReport = report,
            StillViolatesPolicy = stillViolates,
            StatusCode = 200
        };

    public static RecipeRepairResult NotFound(string taskId) =>
        new()
        {
            Success = false,
            StatusCode = 404,
            ErrorCode = "TASK_NOT_FOUND",
            Error = $"Task {taskId} not found"
        };

    public static RecipeRepairResult InvalidState(string taskId, string currentStatus) =>
        new()
        {
            Success = false,
            StatusCode = 400,
            ErrorCode = "INVALID_TASK_STATE",
            Error = $"Task must be in ReviewReady state to repair. Current state: {currentStatus}"
        };

    public static RecipeRepairResult NoDraftFound(string taskId) =>
        new()
        {
            Success = false,
            StatusCode = 400,
            ErrorCode = "NO_DRAFT_FOUND",
            Error = $"No recipe draft found for task {taskId}"
        };

    public static RecipeRepairResult NoRepairNeeded() =>
        new()
        {
            Success = true,
            StatusCode = 200,
            ErrorCode = "NO_REPAIR_NEEDED",
            Error = "The draft does not have high similarity - no repair needed"
        };

    public static RecipeRepairResult Failed(string error) =>
        new()
        {
            Success = false,
            StatusCode = 500,
            ErrorCode = "REPAIR_FAILED",
            Error = error
        };
}

/// <summary>
/// Implementation of the recipe repair service.
/// </summary>
public class RecipeRepairService : IRecipeRepairService
{
    private readonly IMessagingBus _messagingBus;
    private readonly IRepairParaphraseService _repairService;
    private readonly IBlobStorage _blobStorage;
    private readonly ILogger<RecipeRepairService> _logger;

    public RecipeRepairService(
        IMessagingBus messagingBus,
        IRepairParaphraseService repairService,
        IBlobStorage blobStorage,
        ILogger<RecipeRepairService> logger)
    {
        _messagingBus = messagingBus;
        _repairService = repairService;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<RecipeRepairResult> RepairAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting manual repair for task {TaskId}", taskId);

        // Get task state
        var taskState = await _messagingBus.GetTaskStateAsync(taskId, cancellationToken);
        if (taskState == null)
        {
            return RecipeRepairResult.NotFound(taskId);
        }

        // Validate task is in ReviewReady state
        if (taskState.Status != AgentTaskStatus.ReviewReady)
        {
            return RecipeRepairResult.InvalidState(taskId, taskState.Status.ToString());
        }

        // Get the draft from task state result
        if (string.IsNullOrEmpty(taskState.Result))
        {
            return RecipeRepairResult.NoDraftFound(taskId);
        }

        RecipeDraft draft;
        try
        {
            draft = JsonSerializer.Deserialize<RecipeDraft>(taskState.Result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize draft for task {TaskId}", taskId);
            return RecipeRepairResult.Failed($"Failed to parse draft: {ex.Message}");
        }

        // Check if repair is needed
        if (draft.SimilarityReport == null || !draft.SimilarityReport.ViolatesPolicy)
        {
            return RecipeRepairResult.NoRepairNeeded();
        }

        // Get the sanitized source text from blob storage
        // First, try to find the artifact path from the draft's artifacts list
        string? sourceText = null;
        string? successfulPath = null;
        
        // Check if we have the sanitized text artifact reference in the draft
        var sanitizedArtifact = draft.Artifacts?.FirstOrDefault(a => 
            a.Type == "sanitized.txt" || 
            a.Uri?.Contains("sanitized.txt") == true ||
            a.Uri?.Contains("/sanitize/") == true);
        
        if (sanitizedArtifact != null && !string.IsNullOrEmpty(sanitizedArtifact.Uri))
        {
            _logger.LogInformation("Found sanitized artifact reference: {Uri}", sanitizedArtifact.Uri);
            
            // Extract just the blob path from the URI
            // URI can be full URL like: http://127.0.0.1:59286/devstoreaccount1/artifacts/threadId/taskId/sanitize/sanitized.txt
            // We need to extract: threadId/taskId/sanitize/sanitized.txt
            var blobPath = ExtractBlobPath(sanitizedArtifact.Uri);
            _logger.LogDebug("Extracted blob path: {BlobPath} from URI: {Uri}", blobPath, sanitizedArtifact.Uri);
            
            try
            {
                var sourceBytes = await _blobStorage.DownloadAsync(blobPath, cancellationToken);
                if (sourceBytes != null && sourceBytes.Length > 0)
                {
                    sourceText = System.Text.Encoding.UTF8.GetString(sourceBytes);
                    successfulPath = blobPath;
                    _logger.LogInformation("Loaded source text from artifact {Path} ({Length} bytes)", 
                        blobPath, sourceBytes.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load source text from artifact path {Path}", blobPath);
            }
        }
        
        // If artifact reference didn't work, try to extract threadId from any artifact URI
        if (string.IsNullOrEmpty(sourceText))
        {
            // Try to extract threadId from any existing artifact URI
            string? threadId = null;
            
            var anyArtifactWithPath = draft.Artifacts?.FirstOrDefault(a => 
                !string.IsNullOrEmpty(a.Uri));
            
            if (anyArtifactWithPath != null)
            {
                // Extract the blob path and parse the threadId from it
                var blobPath = ExtractBlobPath(anyArtifactWithPath.Uri!);
                var pathParts = blobPath.Split('/');
                if (pathParts.Length >= 2)
                {
                    // First segment is threadId, second is taskId
                    threadId = pathParts[0];
                    _logger.LogInformation("Extracted threadId {ThreadId} from blob path {BlobPath}", 
                        threadId, blobPath);
                }
            }
            
            // Fall back to taskId if we couldn't extract threadId
            threadId ??= taskId;
            
            var possiblePaths = new[]
            {
                $"{threadId}/{taskId}/sanitize/sanitized.txt",
                $"{taskId}/{taskId}/sanitize/sanitized.txt",
                $"{taskId}/sanitize/sanitized.txt"
            };
            
            _logger.LogInformation("Trying {Count} possible paths with threadId={ThreadId}, taskId={TaskId}", 
                possiblePaths.Length, threadId, taskId);
            
            foreach (var path in possiblePaths)
            {
                try
                {
                    _logger.LogDebug("Trying to load source text from path: {Path}", path);
                    var sourceBytes = await _blobStorage.DownloadAsync(path, cancellationToken);
                    if (sourceBytes != null && sourceBytes.Length > 0)
                    {
                        sourceText = System.Text.Encoding.UTF8.GetString(sourceBytes);
                        successfulPath = path;
                        _logger.LogInformation("Loaded source text from {Path} ({Length} bytes)", path, sourceBytes.Length);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not load source text from path {Path}", path);
                }
            }
            
            if (string.IsNullOrEmpty(sourceText))
            {
                _logger.LogError("Could not load source text for task {TaskId}. Tried artifact and paths: {Paths}", 
                    taskId, string.Join(", ", possiblePaths));
                return RecipeRepairResult.Failed(
                    "Could not load source text for comparison. The sanitized content may not have been stored during import.");
            }
        }

        // Attempt repair
        _logger.LogInformation("Starting repair with source text from {Path}", successfulPath);
        
        RepairParaphraseResult repairResult;
        try
        {
            repairResult = await _repairService.RepairAsync(
                draft,
                sourceText,
                draft.SimilarityReport,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repair service threw exception for task {TaskId}", taskId);
            return RecipeRepairResult.Failed($"Repair failed: {ex.Message}");
        }

        if (!repairResult.Success || repairResult.RepairedDraft == null)
        {
            var errorMsg = repairResult.Error ?? "Repair failed";
            _logger.LogWarning("Repair unsuccessful for task {TaskId}: {Error}", taskId, errorMsg);
            return RecipeRepairResult.Failed(errorMsg);
        }

        // Update task state with repaired draft
        var updatedDraftJson = JsonSerializer.Serialize(repairResult.RepairedDraft, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _messagingBus.SetTaskStateAsync(taskId, taskState with
        {
            Result = updatedDraftJson,
            LastUpdated = DateTime.UtcNow
        }, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Repair completed for task {TaskId}. New similarity: {Similarity:P2}, Still violates: {StillViolates}",
            taskId,
            repairResult.NewSimilarityReport?.MaxNgramSimilarity ?? 0,
            repairResult.StillViolatesPolicy);

        return RecipeRepairResult.Ok(
            repairResult.RepairedDraft,
            repairResult.NewSimilarityReport ?? draft.SimilarityReport,
            repairResult.StillViolatesPolicy);
    }

    /// <summary>
    /// Extracts the blob path from a full blob storage URI.
    /// Handles both Azurite (local) and Azure Blob Storage URLs.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Azurite: http://127.0.0.1:59286/devstoreaccount1/artifacts/threadId/taskId/sanitize/sanitized.txt
    ///   Returns: threadId/taskId/sanitize/sanitized.txt
    /// - Azure: https://myaccount.blob.core.windows.net/artifacts/threadId/taskId/sanitize/sanitized.txt
    ///   Returns: threadId/taskId/sanitize/sanitized.txt
    /// - Already a path: threadId/taskId/sanitize/sanitized.txt
    ///   Returns: threadId/taskId/sanitize/sanitized.txt
    /// </remarks>
    private string ExtractBlobPath(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return uri;
            
        // If it doesn't look like a URL, assume it's already a path
        if (!uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        try
        {
            var parsedUri = new Uri(uri);
            var pathSegments = parsedUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // For Azurite: /devstoreaccount1/artifacts/threadId/taskId/...
            // For Azure: /artifacts/threadId/taskId/...
            // We need to skip account name (Azurite) and container name
            
            // Find the container (usually "artifacts") and skip everything before and including it
            var artifactsIndex = Array.FindIndex(pathSegments, s => 
                s.Equals("artifacts", StringComparison.OrdinalIgnoreCase));
            
            if (artifactsIndex >= 0 && artifactsIndex < pathSegments.Length - 1)
            {
                // Return everything after "artifacts"
                return string.Join("/", pathSegments.Skip(artifactsIndex + 1));
            }
            
            // If no "artifacts" container found, skip first segment (account/container) and return rest
            if (pathSegments.Length > 1)
            {
                return string.Join("/", pathSegments.Skip(1));
            }
            
            return pathSegments.Length > 0 ? pathSegments[0] : uri;
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Failed to parse URI {Uri}, using as-is", uri);
            return uri;
        }
    }
}
