using System.Text.Json.Serialization;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Services;

/// <summary>
/// Service for rejecting ingest task drafts.
/// Handles validation and state transitions for the reject workflow.
/// </summary>
public interface ITaskRejectService
{
    /// <summary>
    /// Rejects a task draft, transitioning it to the Rejected terminal state.
    /// </summary>
    Task<RejectTaskResult> RejectAsync(string taskId, string? reason = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request DTO for rejecting a task.
/// </summary>
public record RejectTaskRequest
{
    /// <summary>
    /// Optional reason for rejection.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonProperty("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Response DTO for a successful task rejection.
/// </summary>
public record RejectTaskResponse
{
    /// <summary>
    /// The ID of the rejected task.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// The new status of the task (should be "Rejected").
    /// </summary>
    [JsonPropertyName("taskStatus")]
    [JsonProperty("taskStatus")]
    public required string TaskStatus { get; init; }

    /// <summary>
    /// Human-readable message about the rejection.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Timestamp when the task was rejected.
    /// </summary>
    [JsonPropertyName("rejectedAt")]
    [JsonProperty("rejectedAt")]
    public DateTime RejectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Error response for rejection failures.
/// </summary>
public record RejectTaskError
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public required string Message { get; init; }

    /// <summary>
    /// The task ID that was attempted.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public string? TaskId { get; init; }

    /// <summary>
    /// Current task status if available.
    /// </summary>
    [JsonPropertyName("taskStatus")]
    [JsonProperty("taskStatus")]
    public string? TaskStatus { get; init; }
}

/// <summary>
/// Well-known error codes for reject operations.
/// </summary>
public static class RejectErrorCodes
{
    /// <summary>Task not found.</summary>
    public const string TaskNotFound = "TASK_NOT_FOUND";
    
    /// <summary>Task is not in ReviewReady state.</summary>
    public const string InvalidTaskState = "INVALID_TASK_STATE";
    
    /// <summary>Task is already in a terminal state.</summary>
    public const string AlreadyTerminal = "ALREADY_TERMINAL";
}

/// <summary>
/// Result of a reject operation.
/// </summary>
public record RejectTaskResult
{
    public bool Success { get; init; }
    public RejectTaskResponse? Response { get; init; }
    public RejectTaskError? Error { get; init; }
    public int StatusCode { get; init; }

    public static RejectTaskResult Ok(RejectTaskResponse response) =>
        new() { Success = true, Response = response, StatusCode = 200 };

    public static RejectTaskResult NotFound(string taskId) =>
        new()
        {
            Success = false,
            StatusCode = 404,
            Error = new RejectTaskError
            {
                Code = RejectErrorCodes.TaskNotFound,
                Message = $"Task {taskId} not found",
                TaskId = taskId
            }
        };

    public static RejectTaskResult InvalidState(string taskId, string currentStatus) =>
        new()
        {
            Success = false,
            StatusCode = 400,
            Error = new RejectTaskError
            {
                Code = RejectErrorCodes.InvalidTaskState,
                Message = $"Task must be in ReviewReady state to reject. Current state: {currentStatus}",
                TaskId = taskId,
                TaskStatus = currentStatus
            }
        };

    public static RejectTaskResult AlreadyRejected(string taskId) =>
        new()
        {
            Success = true,
            StatusCode = 200,
            Response = new RejectTaskResponse
            {
                TaskId = taskId,
                TaskStatus = "Rejected",
                Message = "Task was already rejected (idempotent response)"
            }
        };

    public static RejectTaskResult AlreadyTerminal(string taskId, string currentStatus) =>
        new()
        {
            Success = false,
            StatusCode = 400,
            Error = new RejectTaskError
            {
                Code = RejectErrorCodes.AlreadyTerminal,
                Message = $"Task is already in terminal state: {currentStatus}",
                TaskId = taskId,
                TaskStatus = currentStatus
            }
        };
}

/// <summary>
/// Implementation of the task reject service.
/// </summary>
public class TaskRejectService : ITaskRejectService
{
    private readonly TaskRepository _taskRepository;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<TaskRejectService> _logger;

    public TaskRejectService(
        TaskRepository taskRepository,
        IMessagingBus messagingBus,
        ILogger<TaskRejectService> logger)
    {
        _taskRepository = taskRepository;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    public async Task<RejectTaskResult> RejectAsync(string taskId, string? reason = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reject for task {TaskId}", taskId);

        // Step 1: Verify task exists
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        
        if (task == null)
        {
            _logger.LogWarning("Reject failed: Task {TaskId} not found", taskId);
            return RejectTaskResult.NotFound(taskId);
        }

        // Step 2: Get current task state from Redis
        var taskState = await _messagingBus.GetTaskStateAsync(taskId, cancellationToken);

        // Step 3: Check for idempotency - already rejected
        if (taskState?.Status == AgentTaskStatus.Rejected)
        {
            _logger.LogInformation("Idempotent reject: Task {TaskId} already rejected", taskId);
            return RejectTaskResult.AlreadyRejected(taskId);
        }

        // Step 4: Check for other terminal states (Committed, Expired)
        if (taskState?.Status == AgentTaskStatus.Committed)
        {
            _logger.LogWarning("Reject failed: Task {TaskId} is already committed", taskId);
            return RejectTaskResult.AlreadyTerminal(taskId, "Committed");
        }

        if (taskState?.Status == AgentTaskStatus.Expired)
        {
            _logger.LogWarning("Reject failed: Task {TaskId} is already expired", taskId);
            return RejectTaskResult.AlreadyTerminal(taskId, "Expired");
        }

        // Step 5: Validate task is in ReviewReady state
        if (taskState?.Status != AgentTaskStatus.ReviewReady)
        {
            var currentStatus = taskState?.Status.ToString() ?? "Unknown";
            _logger.LogWarning("Reject failed: Task {TaskId} is in {Status} state, not ReviewReady", taskId, currentStatus);
            return RejectTaskResult.InvalidState(taskId, currentStatus);
        }

        // Step 6: Transition to Rejected state
        var now = DateTime.UtcNow;
        await _messagingBus.SetTaskStateAsync(taskId, new TaskState
        {
            TaskId = taskId,
            Status = AgentTaskStatus.Rejected,
            Progress = taskState.Progress,
            CurrentPhase = "Rejected",
            Result = reason,
            LastUpdated = now
        }, null, cancellationToken);

        // Step 7: Update task metadata with rejection info
        await _taskRepository.UpdateMetadataAsync(taskId, new Dictionary<string, string>
        {
            ["rejectedAt"] = now.ToString("O"),
            ["rejectionReason"] = reason ?? ""
        }, cancellationToken);

        _logger.LogInformation("Task {TaskId} rejected successfully", taskId);

        return RejectTaskResult.Ok(new RejectTaskResponse
        {
            TaskId = taskId,
            TaskStatus = "Rejected",
            Message = reason ?? "Task rejected by user",
            RejectedAt = now
        });
    }
}
