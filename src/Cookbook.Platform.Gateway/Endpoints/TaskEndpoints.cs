using System.Text.Json;
using Cookbook.Platform.Shared.Agents;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Storage.Repositories;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Task management endpoints.
/// </summary>
public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tasks")
            .WithTags("Tasks");

        group.MapPost("/", CreateTask)
            .WithName("CreateTask")
            .WithSummary("Creates and enqueues a new agent task");

        group.MapGet("/{taskId}", GetTask)
            .WithName("GetTask")
            .WithSummary("Gets a task by ID");

        group.MapGet("/{taskId}/state", GetTaskState)
            .WithName("GetTaskState")
            .WithSummary("Gets the current state of a task");

        group.MapPost("/{taskId}/cancel", CancelTask)
            .WithName("CancelTask")
            .WithSummary("Cancels a running task");

        group.MapGet("/{taskId}/artifacts", GetTaskArtifacts)
            .WithName("GetTaskArtifacts")
            .WithSummary("Gets artifacts produced by a task");

        return endpoints;
    }

    public record CreateTaskRequest(string ThreadId, string AgentType, string Query);

    private static async Task<IResult> CreateTask(
        CreateTaskRequest request,
        IMessagingBus messagingBus,
        TaskRepository taskRepository,
        CancellationToken cancellationToken)
    {
        // Validate agent type
        if (!KnownAgentTypes.IsValid(request.AgentType))
        {
            return Results.BadRequest(new
            {
                error = "INVALID_AGENT_TYPE",
                message = $"Unknown agent type '{request.AgentType}'. Valid types are: {string.Join(", ", KnownAgentTypes.All)}",
                validTypes = KnownAgentTypes.All
            });
        }

        // Normalize agent type to canonical casing
        var agentType = KnownAgentTypes.GetCanonical(request.AgentType) ?? request.AgentType;

        // Determine payload based on whether Query is already JSON or a plain string
        string payload;
        if (request.Query.TrimStart().StartsWith("{") || request.Query.TrimStart().StartsWith("["))
        {
            // Already JSON (e.g., for Analysis tasks with RecipeId)
            payload = request.Query;
        }
        else
        {
            // Plain string query (e.g., for Research tasks)
            payload = JsonSerializer.Serialize(new { Query = request.Query });
        }

        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            ThreadId = request.ThreadId,
            AgentType = agentType,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };

        // Persist task to Cosmos
        await taskRepository.CreateAsync(task, cancellationToken);

        // Enqueue task via messaging bus
        await messagingBus.SendTaskAsync(task, cancellationToken);

        return Results.Created($"/api/tasks/{task.TaskId}", task);
    }

    private static async Task<IResult> GetTask(
        string taskId,
        TaskRepository taskRepository,
        CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(taskId, cancellationToken);
        return task is null ? Results.NotFound() : Results.Ok(task);
    }

    private static async Task<IResult> GetTaskState(
        string taskId,
        IMessagingBus messagingBus,
        CancellationToken cancellationToken)
    {
        var state = await messagingBus.GetTaskStateAsync(taskId, cancellationToken);
        return state is null ? Results.NotFound() : Results.Ok(state);
    }

    private static async Task<IResult> CancelTask(
        string taskId,
        IMessagingBus messagingBus,
        CancellationToken cancellationToken)
    {
        await messagingBus.CancelTaskAsync(taskId, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> GetTaskArtifacts(
        string taskId,
        ArtifactRepository artifactRepository,
        CancellationToken cancellationToken)
    {
        var artifacts = await artifactRepository.GetByTaskIdAsync(taskId, cancellationToken);
        return Results.Ok(artifacts);
    }
}
