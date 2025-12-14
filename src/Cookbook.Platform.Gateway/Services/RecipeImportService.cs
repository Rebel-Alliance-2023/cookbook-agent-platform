using System.Text.Json;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Utilities;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Services;

/// <summary>
/// Service for importing (committing) recipe drafts to the permanent recipe collection.
/// Handles validation, idempotency, concurrency, expiration, and duplicate detection.
/// </summary>
public interface IRecipeImportService
{
    /// <summary>
    /// Imports a recipe draft as a permanent recipe.
    /// </summary>
    Task<ImportRecipeResult> ImportAsync(ImportRecipeRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an import operation.
/// </summary>
public record ImportRecipeResult
{
    public bool Success { get; init; }
    public ImportRecipeResponse? Response { get; init; }
    public ImportRecipeError? Error { get; init; }
    public int StatusCode { get; init; }
    
    /// <summary>
    /// Indicates this was an idempotent success (already committed).
    /// </summary>
    public bool WasIdempotent { get; init; }

    public static ImportRecipeResult Ok(ImportRecipeResponse response, bool wasIdempotent = false) => 
        new() { Success = true, Response = response, StatusCode = wasIdempotent ? 200 : 201, WasIdempotent = wasIdempotent };
    
    public static ImportRecipeResult NotFound(string taskId) =>
        new() 
        { 
            Success = false, 
            StatusCode = 404, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.TaskNotFound, 
                Message = $"Task {taskId} not found",
                TaskId = taskId
            } 
        };
    
    public static ImportRecipeResult InvalidState(string taskId, string currentStatus) =>
        new() 
        { 
            Success = false, 
            StatusCode = 400, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.InvalidTaskState, 
                Message = $"Task must be in ReviewReady state to commit. Current state: {currentStatus}",
                TaskId = taskId,
                TaskStatus = currentStatus
            } 
        };
    
    public static ImportRecipeResult Expired(string taskId) =>
        new() 
        { 
            Success = false, 
            StatusCode = 410, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.DraftExpired, 
                Message = "The draft has expired and can no longer be committed",
                TaskId = taskId,
                TaskStatus = "Expired"
            } 
        };
    
    public static ImportRecipeResult Conflict(string taskId, string message) =>
        new() 
        { 
            Success = false, 
            StatusCode = 409, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.ConcurrencyConflict, 
                Message = message,
                TaskId = taskId
            } 
        };
    
    public static ImportRecipeResult Rejected(string taskId) =>
        new() 
        { 
            Success = false, 
            StatusCode = 400, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.TaskRejected, 
                Message = "Task was rejected and cannot be committed",
                TaskId = taskId,
                TaskStatus = "Rejected"
            } 
        };
    
    public static ImportRecipeResult InvalidDraft(string taskId, string details) =>
        new() 
        { 
            Success = false, 
            StatusCode = 400, 
            Error = new ImportRecipeError 
            { 
                Code = ImportErrorCodes.InvalidDraft, 
                Message = $"Draft data is invalid: {details}",
                TaskId = taskId
            } 
        };
}

/// <summary>
/// Implementation of the recipe import service.
/// </summary>
public class RecipeImportService : IRecipeImportService
{
    private readonly TaskRepository _taskRepository;
    private readonly RecipeRepository _recipeRepository;
    private readonly IMessagingBus _messagingBus;
    private readonly IngestOptions _options;
    private readonly ILogger<RecipeImportService> _logger;

    public RecipeImportService(
        TaskRepository taskRepository,
        RecipeRepository recipeRepository,
        IMessagingBus messagingBus,
        IOptions<IngestOptions> options,
        ILogger<RecipeImportService> logger)
    {
        _taskRepository = taskRepository;
        _recipeRepository = recipeRepository;
        _messagingBus = messagingBus;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImportRecipeResult> ImportAsync(ImportRecipeRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting import for task {TaskId}", request.TaskId);

        // Step 1: Get task with ETag
        var (task, currentETag) = await _taskRepository.GetByIdWithETagAsync(request.TaskId, cancellationToken);
        
        if (task == null)
        {
            _logger.LogWarning("Import failed: Task {TaskId} not found", request.TaskId);
            return ImportRecipeResult.NotFound(request.TaskId);
        }

        // Step 2: Get current task state from Redis
        var taskState = await _messagingBus.GetTaskStateAsync(request.TaskId, cancellationToken);

        // Step 3: Check for idempotency - already committed
        if (taskState?.Status == AgentTaskStatus.Committed)
        {
            _logger.LogInformation("Idempotent import: Task {TaskId} already committed", request.TaskId);
            return await HandleIdempotentCommit(task, taskState, cancellationToken);
        }

        // Step 4: Check for rejected state
        if (taskState?.Status == AgentTaskStatus.Rejected)
        {
            _logger.LogWarning("Import failed: Task {TaskId} was rejected", request.TaskId);
            return ImportRecipeResult.Rejected(request.TaskId);
        }

        // Step 5: Validate task is in ReviewReady state
        if (taskState?.Status != AgentTaskStatus.ReviewReady)
        {
            var currentStatus = taskState?.Status.ToString() ?? "Unknown";
            _logger.LogWarning("Import failed: Task {TaskId} is in {Status} state, not ReviewReady", request.TaskId, currentStatus);
            return ImportRecipeResult.InvalidState(request.TaskId, currentStatus);
        }

        // Step 6: Check for expiration
        if (IsExpired(task, taskState))
        {
            _logger.LogWarning("Import failed: Task {TaskId} has expired", request.TaskId);
            
            // Update state to Expired
            await _messagingBus.SetTaskStateAsync(request.TaskId, taskState with 
            { 
                Status = AgentTaskStatus.Expired,
                LastUpdated = DateTime.UtcNow
            }, null, cancellationToken);
            
            return ImportRecipeResult.Expired(request.TaskId);
        }

        // Step 7: Check ETag for optimistic concurrency
        if (!string.IsNullOrEmpty(request.ETag) && request.ETag != currentETag)
        {
            _logger.LogWarning("Import failed: ETag mismatch for task {TaskId}. Expected: {Expected}, Current: {Current}", 
                request.TaskId, request.ETag, currentETag);
            return ImportRecipeResult.Conflict(request.TaskId, "The draft was modified by another request. Please refresh and try again.");
        }

        // Step 8: Parse and validate the draft from payload
        var draftResult = ParseRecipeDraft(task);
        if (!draftResult.Success)
        {
            return ImportRecipeResult.InvalidDraft(request.TaskId, draftResult.Error!);
        }

        var draft = draftResult.Draft!;
        var warnings = new List<string>();

        // Step 9: Ensure UrlHash is computed
        var urlHash = EnsureUrlHash(draft.Source);

        // Step 10: Check for duplicates
        var (duplicateDetected, duplicateRecipeId) = await CheckForDuplicate(urlHash, cancellationToken);
        if (duplicateDetected)
        {
            warnings.Add($"A recipe from this URL already exists (ID: {duplicateRecipeId})");
            _logger.LogInformation("Duplicate detected for task {TaskId}: existing recipe {RecipeId}", request.TaskId, duplicateRecipeId);
        }

        // Step 11: Create the recipe with proper ID and timestamps
        var recipe = CreateRecipe(draft, request.Overrides, urlHash);

        // Step 12: Persist to Cosmos
        try
        {
            var createdRecipe = await _recipeRepository.CreateAsync(recipe, cancellationToken);
            _logger.LogInformation("Created recipe {RecipeId} from task {TaskId}", createdRecipe.Id, request.TaskId);

            // Step 13: Update task metadata with committed recipe ID
            await _taskRepository.UpdateMetadataAsync(request.TaskId, new Dictionary<string, string>
            {
                ["committedRecipeId"] = createdRecipe.Id,
                ["committedAt"] = DateTime.UtcNow.ToString("O")
            }, cancellationToken);

            // Step 14: Update task state to Committed
            await _messagingBus.SetTaskStateAsync(request.TaskId, new TaskState
            {
                TaskId = request.TaskId,
                Status = AgentTaskStatus.Committed,
                Progress = 100,
                CurrentPhase = "Committed",
                Result = createdRecipe.Id,
                LastUpdated = DateTime.UtcNow
            }, null, cancellationToken);

            _logger.LogInformation("Task {TaskId} committed successfully, recipe {RecipeId}", request.TaskId, createdRecipe.Id);

            return ImportRecipeResult.Ok(new ImportRecipeResponse
            {
                RecipeId = createdRecipe.Id,
                TaskId = request.TaskId,
                TaskStatus = "Committed",
                RecipeName = createdRecipe.Name,
                UrlHash = urlHash,
                Warnings = warnings,
                DuplicateDetected = duplicateDetected,
                DuplicateRecipeId = duplicateRecipeId,
                CreatedAt = createdRecipe.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist recipe for task {TaskId}", request.TaskId);
            return new ImportRecipeResult
            {
                Success = false,
                StatusCode = 500,
                Error = new ImportRecipeError
                {
                    Code = ImportErrorCodes.PersistenceFailed,
                    Message = "Failed to save the recipe. Please try again.",
                    TaskId = request.TaskId,
                    Details = new Dictionary<string, object> { ["exception"] = ex.Message }
                }
            };
        }
    }

    private async Task<ImportRecipeResult> HandleIdempotentCommit(AgentTask task, TaskState taskState, CancellationToken cancellationToken)
    {
        // Get the committed recipe ID from metadata or result
        var recipeId = task.Metadata.GetValueOrDefault("committedRecipeId") ?? taskState.Result;
        
        if (string.IsNullOrEmpty(recipeId))
        {
            return ImportRecipeResult.InvalidDraft(task.TaskId, "Task marked as committed but no recipe ID found");
        }

        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        
        return ImportRecipeResult.Ok(new ImportRecipeResponse
        {
            RecipeId = recipeId,
            TaskId = task.TaskId,
            TaskStatus = "Committed",
            RecipeName = recipe?.Name ?? "Unknown",
            UrlHash = recipe?.Source?.UrlHash,
            Warnings = ["This task was already committed (idempotent response)"],
            DuplicateDetected = false,
            CreatedAt = recipe?.CreatedAt ?? DateTime.UtcNow
        }, wasIdempotent: true);
    }

    private bool IsExpired(AgentTask task, TaskState? taskState)
    {
        // Check if we transitioned to ReviewReady and have been there too long
        var reviewReadyTime = taskState?.LastUpdated ?? task.CreatedAt;
        var expirationTime = reviewReadyTime.Add(_options.DraftExpiration);
        
        return DateTime.UtcNow > expirationTime;
    }

    private (bool Success, RecipeDraft? Draft, string? Error) ParseRecipeDraft(AgentTask task)
    {
        try
        {
            // The draft should be stored in task metadata or payload
            var draftJson = task.Metadata.GetValueOrDefault("recipeDraft");
            
            if (string.IsNullOrEmpty(draftJson))
            {
                // Try to extract from the IngestPayload result
                var payload = JsonSerializer.Deserialize<IngestPayload>(task.Payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // For now, check if there's a draft in extended metadata
                draftJson = task.Metadata.GetValueOrDefault("draft.recipe.json");
            }

            if (string.IsNullOrEmpty(draftJson))
            {
                return (false, null, "No recipe draft found in task");
            }

            var draft = JsonSerializer.Deserialize<RecipeDraft>(draftJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (draft == null)
            {
                return (false, null, "Failed to deserialize recipe draft");
            }

            if (draft.Recipe == null)
            {
                return (false, null, "Recipe draft contains no recipe data");
            }

            return (true, draft, null);
        }
        catch (JsonException ex)
        {
            return (false, null, $"Invalid JSON in recipe draft: {ex.Message}");
        }
    }

    private string EnsureUrlHash(RecipeSource source)
    {
        if (!string.IsNullOrEmpty(source.UrlHash))
        {
            return source.UrlHash;
        }

        // Compute hash from URL
        return UrlHasher.ComputeHash(source.Url);
    }

    private async Task<(bool Found, string? RecipeId)> CheckForDuplicate(string urlHash, CancellationToken cancellationToken)
    {
        var existingRecipe = await _recipeRepository.FindDuplicateByUrlHashAsync(urlHash, cancellationToken);
        return existingRecipe != null 
            ? (true, existingRecipe.Id) 
            : (false, null);
    }

    private Recipe CreateRecipe(RecipeDraft draft, RecipeOverrides? overrides, string urlHash)
    {
        var source = draft.Source with { UrlHash = urlHash };
        var now = DateTime.UtcNow;
        var recipeId = Guid.NewGuid().ToString();

        // Start with the draft recipe and apply overrides
        var recipe = draft.Recipe with
        {
            Id = recipeId,
            CreatedAt = now,
            UpdatedAt = null,
            Source = source,
            // Apply overrides if provided
            Name = overrides?.Name ?? draft.Recipe.Name,
            Description = overrides?.Description ?? draft.Recipe.Description,
            Cuisine = overrides?.Cuisine ?? draft.Recipe.Cuisine,
            DietType = overrides?.DietType ?? draft.Recipe.DietType,
            Tags = overrides?.Tags ?? draft.Recipe.Tags
        };

        return recipe;
    }
}
