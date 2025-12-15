using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;
using MessagingTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Services;

/// <summary>
/// Implementation of the patch application service.
/// </summary>
public class PatchApplicationService : IPatchApplicationService
{
    private readonly TaskRepository _taskRepository;
    private readonly RecipeRepository _recipeRepository;
    private readonly INormalizeService _normalizeService;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<PatchApplicationService> _logger;

    public PatchApplicationService(
        TaskRepository taskRepository,
        RecipeRepository recipeRepository,
        INormalizeService normalizeService,
        IMessagingBus messagingBus,
        ILogger<PatchApplicationService> logger)
    {
        _taskRepository = taskRepository;
        _recipeRepository = recipeRepository;
        _normalizeService = normalizeService;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    public async Task<PatchApplicationResult> ApplyPatchAsync(
        string recipeId, 
        ApplyPatchRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying patches for recipe {RecipeId} from task {TaskId}", 
            recipeId, request.TaskId);

        // 1. Validate task exists and is in correct state
        var taskState = await _messagingBus.GetTaskStateAsync(request.TaskId, cancellationToken);
        if (taskState == null)
        {
            return new PatchApplicationResult
            {
                StatusCode = 404,
                Error = new PatchApplicationError
                {
                    Code = "TASK_NOT_FOUND",
                    Message = $"Task '{request.TaskId}' not found",
                    TaskId = request.TaskId
                }
            };
        }

        // Task must be in ReviewReady state for normalize mode
        if (taskState.Status != MessagingTaskStatus.ReviewReady)
        {
            return new PatchApplicationResult
            {
                StatusCode = 400,
                Error = new PatchApplicationError
                {
                    Code = "INVALID_TASK_STATE",
                    Message = $"Task must be in ReviewReady state. Current state: {taskState.Status}",
                    TaskId = request.TaskId
                }
            };
        }

        // 2. Fetch recipe
        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe == null)
        {
            return new PatchApplicationResult
            {
                StatusCode = 404,
                Error = new PatchApplicationError
                {
                    Code = "RECIPE_NOT_FOUND",
                    Message = $"Recipe '{recipeId}' not found",
                    RecipeId = recipeId
                }
            };
        }

        // 3. Get patches from task result
        NormalizePatchResponse? patchResponse = null;
        if (!string.IsNullOrEmpty(taskState.Result))
        {
            try
            {
                var draft = JsonSerializer.Deserialize<RecipeDraft>(taskState.Result, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                patchResponse = draft?.NormalizePatchResponse;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse task result for task {TaskId}", request.TaskId);
            }
        }

        if (patchResponse == null || patchResponse.Patches.Count == 0)
        {
            return new PatchApplicationResult
            {
                StatusCode = 400,
                Error = new PatchApplicationError
                {
                    Code = "NO_PATCHES",
                    Message = "No patches found in task result",
                    TaskId = request.TaskId
                }
            };
        }

        // 4. Filter patches based on request
        var patchesToApply = FilterPatches(patchResponse.Patches, request);
        var skippedCount = patchResponse.Patches.Count - patchesToApply.Count;

        if (patchesToApply.Count == 0)
        {
            return new PatchApplicationResult
            {
                StatusCode = 200,
                Response = new ApplyPatchResponse
                {
                    Success = true,
                    RecipeId = recipeId,
                    AppliedCount = 0,
                    FailedCount = 0,
                    SkippedCount = skippedCount,
                    Summary = "No patches to apply after filtering"
                }
            };
        }

        // 5. Apply patches
        var applyResult = await _normalizeService.ApplyPatchesAsync(recipe, patchesToApply, cancellationToken);

        // 6. Persist updated recipe if any patches were applied
        if (applyResult.AppliedPatches.Count > 0 && applyResult.NormalizedRecipe != null)
        {
            try
            {
                var updatedRecipe = applyResult.NormalizedRecipe with
                {
                    UpdatedAt = DateTime.UtcNow
                };
                await _recipeRepository.UpdateAsync(updatedRecipe, cancellationToken);
                
                _logger.LogInformation("Updated recipe {RecipeId} with {PatchCount} patches", 
                    recipeId, applyResult.AppliedPatches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist updated recipe {RecipeId}", recipeId);
                return new PatchApplicationResult
                {
                    StatusCode = 500,
                    Error = new PatchApplicationError
                    {
                        Code = "PERSIST_FAILED",
                        Message = $"Failed to persist updated recipe: {ex.Message}",
                        RecipeId = recipeId
                    }
                };
            }
        }

        // 7. Update task state
        await _messagingBus.SetTaskStateAsync(request.TaskId, taskState with
        {
            Status = MessagingTaskStatus.Committed,
            LastUpdated = DateTime.UtcNow
        }, cancellationToken: cancellationToken);

        // 8. Build response
        var errors = applyResult.FailedPatches
            .Select(f => $"{f.Patch.Path}: {f.Error}")
            .ToList();

        return new PatchApplicationResult
        {
            StatusCode = 200,
            Response = new ApplyPatchResponse
            {
                Success = applyResult.Success,
                RecipeId = recipeId,
                AppliedCount = applyResult.AppliedPatches.Count,
                FailedCount = applyResult.FailedPatches.Count,
                SkippedCount = skippedCount,
                Summary = applyResult.Summary,
                Errors = errors
            }
        };
    }

    public async Task<PatchRejectionResult> RejectPatchAsync(
        string recipeId, 
        RejectPatchRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rejecting patches for recipe {RecipeId} from task {TaskId}", 
            recipeId, request.TaskId);

        // 1. Validate task exists and is in correct state
        var taskState = await _messagingBus.GetTaskStateAsync(request.TaskId, cancellationToken);
        if (taskState == null)
        {
            return new PatchRejectionResult
            {
                StatusCode = 404,
                Error = new PatchApplicationError
                {
                    Code = "TASK_NOT_FOUND",
                    Message = $"Task '{request.TaskId}' not found",
                    TaskId = request.TaskId
                }
            };
        }

        // Task must be in ReviewReady state
        if (taskState.Status != MessagingTaskStatus.ReviewReady)
        {
            return new PatchRejectionResult
            {
                StatusCode = 400,
                Error = new PatchApplicationError
                {
                    Code = "INVALID_TASK_STATE",
                    Message = $"Task must be in ReviewReady state. Current state: {taskState.Status}",
                    TaskId = request.TaskId
                }
            };
        }

        // 2. Verify recipe exists (but don't modify it)
        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe == null)
        {
            return new PatchRejectionResult
            {
                StatusCode = 404,
                Error = new PatchApplicationError
                {
                    Code = "RECIPE_NOT_FOUND",
                    Message = $"Recipe '{recipeId}' not found",
                    RecipeId = recipeId
                }
            };
        }

        // 3. Update task state to Rejected
        await _messagingBus.SetTaskStateAsync(request.TaskId, taskState with
        {
            Status = MessagingTaskStatus.Rejected,
            LastUpdated = DateTime.UtcNow
        }, cancellationToken: cancellationToken);

        _logger.LogInformation("Rejected patches for task {TaskId}, recipe {RecipeId} unchanged", 
            request.TaskId, recipeId);

        return new PatchRejectionResult
        {
            StatusCode = 200,
            Response = new RejectPatchResponse
            {
                Success = true,
                RecipeId = recipeId,
                Message = $"Patches rejected. Recipe '{recipeId}' left unchanged."
            }
        };
    }

    /// <summary>
    /// Filters patches based on request parameters.
    /// </summary>
    private static List<NormalizePatchOperation> FilterPatches(
        IReadOnlyList<NormalizePatchOperation> patches,
        ApplyPatchRequest request)
    {
        var result = new List<NormalizePatchOperation>();

        for (int i = 0; i < patches.Count; i++)
        {
            var patch = patches[i];

            // Filter by indices if specified
            if (request.PatchIndices != null && request.PatchIndices.Count > 0)
            {
                if (!request.PatchIndices.Contains(i))
                    continue;
            }

            // Filter by max risk level if specified
            if (request.MaxRiskLevel.HasValue)
            {
                if (patch.RiskCategory > request.MaxRiskLevel.Value)
                    continue;
            }

            result.Add(patch);
        }

        return result;
    }
}
