using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Agents;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Main orchestrator service that coordinates the agent pipeline.
/// </summary>
public class OrchestratorService
{
    private readonly AgentPipeline _pipeline;
    private readonly IngestPhaseRunner _ingestRunner;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        AgentPipeline pipeline,
        IngestPhaseRunner ingestRunner,
        IMessagingBus messagingBus,
        ILogger<OrchestratorService> logger)
    {
        _pipeline = pipeline;
        _ingestRunner = ingestRunner;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Processes a task through the appropriate pipeline phase.
    /// </summary>
    public async Task ProcessTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting orchestration for task {TaskId} of type {AgentType}", 
            task.TaskId, task.AgentType);

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
            if (string.Equals(task.AgentType, KnownAgentTypes.Ingest, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessIngestTaskAsync(task, cancellationToken);
            }
            else if (string.Equals(task.AgentType, KnownAgentTypes.Research, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessResearchTaskAsync(task, cancellationToken);
            }
            else if (string.Equals(task.AgentType, KnownAgentTypes.Analysis, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessAnalysisTaskAsync(task, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Unknown agent type: {task.AgentType}");
            }

            _logger.LogInformation("Task {TaskId} orchestration completed", task.TaskId);
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

    /// <summary>
    /// Processes an Ingest task through the ingest pipeline.
    /// </summary>
    private async Task ProcessIngestTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var result = await _ingestRunner.ExecuteAsync(task, cancellationToken);

        if (result.Success)
        {
            // Ingest tasks complete to ReviewReady status (not Completed)
            await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
            {
                TaskId = task.TaskId,
                Status = Shared.Messaging.TaskStatus.ReviewReady,
                CurrentPhase = IngestPhases.ReviewReady,
                Progress = 100,
                Result = result.Draft is not null 
                    ? JsonSerializer.Serialize(result.Draft)
                    : null
            }, cancellationToken: cancellationToken);

            // Publish review ready event
            await PublishEventAsync(task.ThreadId, "ingest.review_ready", new
            {
                task.TaskId,
                task.AgentType,
                Draft = result.Draft,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            // Ingest failed
            await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
            {
                TaskId = task.TaskId,
                Status = Shared.Messaging.TaskStatus.Failed,
                CurrentPhase = result.FailedPhase ?? "Unknown",
                Error = result.Error
            }, cancellationToken: cancellationToken);

            // Publish failure event with error code
            await PublishEventAsync(task.ThreadId, "ingest.failed", new
            {
                task.TaskId,
                task.AgentType,
                Error = result.Error,
                ErrorCode = result.ErrorCode,
                FailedPhase = result.FailedPhase,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            throw new InvalidOperationException($"Ingest pipeline failed: {result.Error}");
        }
    }

    /// <summary>
    /// Processes a Research task.
    /// </summary>
    private async Task ProcessResearchTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var result = await _pipeline.ExecuteResearchPhaseAsync(task, cancellationToken);

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
    }

    /// <summary>
    /// Processes an Analysis task.
    /// </summary>
    private async Task ProcessAnalysisTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var result = await _pipeline.ExecuteAnalysisPhaseAsync(task, cancellationToken);

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
