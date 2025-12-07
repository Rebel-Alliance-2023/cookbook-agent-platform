using System.Text.Json;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Main orchestrator service that coordinates the agent pipeline.
/// </summary>
public class OrchestratorService
{
    private readonly AgentPipeline _pipeline;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        AgentPipeline pipeline,
        IMessagingBus messagingBus,
        ILogger<OrchestratorService> logger)
    {
        _pipeline = pipeline;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Processes a task through the appropriate pipeline phase.
    /// </summary>
    public async Task ProcessTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting orchestration for task {TaskId}", task.TaskId);

        try
        {
            // Update task state to running
            await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
            {
                TaskId = task.TaskId,
                Status = Shared.Messaging.TaskStatus.Running,
                CurrentPhase = "Initializing",
                Progress = 0
            }, cancellationToken: cancellationToken);

            // Publish start event
            await PublishEventAsync(task.ThreadId, "task.started", new
            {
                task.TaskId,
                task.AgentType,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            // Execute the pipeline based on agent type
            object result;
            if (task.AgentType == "Research")
            {
                result = await _pipeline.ExecuteResearchPhaseAsync(task, cancellationToken);
            }
            else if (task.AgentType == "Analysis")
            {
                result = await _pipeline.ExecuteAnalysisPhaseAsync(task, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Unknown agent type: {task.AgentType}");
            }

            // Update task state to completed
            await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
            {
                TaskId = task.TaskId,
                Status = Shared.Messaging.TaskStatus.Completed,
                CurrentPhase = "Completed",
                Progress = 100,
                Result = JsonSerializer.Serialize(result)
            }, cancellationToken: cancellationToken);

            // Publish completion event
            await PublishEventAsync(task.ThreadId, "task.completed", new
            {
                task.TaskId,
                task.AgentType,
                Result = result,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            _logger.LogInformation("Task {TaskId} completed successfully", task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed", task.TaskId);

            // Update task state to failed
            await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
            {
                TaskId = task.TaskId,
                Status = Shared.Messaging.TaskStatus.Failed,
                CurrentPhase = "Failed",
                Error = ex.Message
            }, cancellationToken: cancellationToken);

            // Publish failure event
            await PublishEventAsync(task.ThreadId, "task.failed", new
            {
                task.TaskId,
                task.AgentType,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private async Task PublishEventAsync<T>(string threadId, string eventType, T payload, CancellationToken cancellationToken)
    {
        var agentEvent = new AgentEvent
        {
            EventId = Guid.NewGuid().ToString(),
            ThreadId = threadId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Timestamp = DateTime.UtcNow
        };

        await _messagingBus.PublishEventAsync(threadId, agentEvent, cancellationToken);
    }
}
