using System.Text.Json;
using Cookbook.Platform.Gateway.Services;
using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Agents;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models.Ingest;
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

        group.MapPost("/ingest", CreateIngestTask)
            .WithName("CreateIngestTask")
            .WithSummary("Creates and enqueues a new ingest agent task with full contract support");

        group.MapGet("/{taskId}", GetTask)
            .WithName("GetTask")
            .WithSummary("Gets a task by ID");

        group.MapGet("/{taskId}/state", GetTaskState)
            .WithName("GetTaskState")
            .WithSummary("Gets the current state of a task");

        group.MapPost("/{taskId}/cancel", CancelTask)
            .WithName("CancelTask")
            .WithSummary("Cancels a running task");

        group.MapPost("/{taskId}/reject", RejectTask)
            .WithName("RejectTask")
            .WithSummary("Rejects a task in ReviewReady state, transitioning it to the Rejected terminal state")
            .WithDescription("Only tasks in ReviewReady state can be rejected. " +
                           "Rejected tasks cannot be committed. This operation is idempotent.");

        group.MapGet("/{taskId}/artifacts", GetTaskArtifacts)
            .WithName("GetTaskArtifacts")
            .WithSummary("Gets artifacts produced by a task");

        return endpoints;
    }

    public record CreateTaskRequest(string? ThreadId, string AgentType, string Query);

    /// <summary>
    /// Creates a new ingest task with the full ingest contract.
    /// </summary>
    private static async Task<IResult> CreateIngestTask(
        CreateIngestTaskRequest request,
        IMessagingBus messagingBus,
        TaskRepository taskRepository,
        ISearchProviderResolver searchProviderResolver,
        CancellationToken cancellationToken)
    {
        // Validate agent type is Ingest
        if (!string.Equals(request.AgentType, KnownAgentTypes.Ingest, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                code = "INVALID_AGENT_TYPE",
                message = $"This endpoint only accepts AgentType='{KnownAgentTypes.Ingest}'. Received: '{request.AgentType}'",
                agentType = request.AgentType
            });
        }

        // Validate ingest payload
        var validationResult = ValidateIngestPayload(request.Payload);
        if (validationResult is not null)
        {
            return validationResult;
        }

        // Validate and resolve search provider for Query mode
        string? resolvedProviderId = null;
        if (request.Payload.Mode == IngestMode.Query)
        {
            var providerValidation = ValidateAndResolveSearchProvider(
                request.Payload.Search?.ProviderId, 
                searchProviderResolver);
            
            if (providerValidation.Error is not null)
            {
                return providerValidation.Error;
            }
            
            resolvedProviderId = providerValidation.ProviderId;
        }

        // Generate ThreadId if not provided
        var threadId = string.IsNullOrWhiteSpace(request.ThreadId) 
            ? Guid.NewGuid().ToString() 
            : request.ThreadId;

        // Build final payload with resolved provider if needed
        var finalPayload = resolvedProviderId is not null 
            ? request.Payload with 
            { 
                Search = (request.Payload.Search ?? new SearchSettings()) with 
                { 
                    ProviderId = resolvedProviderId 
                } 
            }
            : request.Payload;

        // Serialize the ingest payload
        var payload = JsonSerializer.Serialize(finalPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            ThreadId = threadId,
            AgentType = KnownAgentTypes.Ingest,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            Metadata = BuildIngestMetadata(finalPayload, resolvedProviderId)
        };

        // Persist task to Cosmos
        await taskRepository.CreateAsync(task, cancellationToken);

        // Enqueue task via messaging bus
        await messagingBus.SendTaskAsync(task, cancellationToken);

        // Return the ingest-specific response
        var response = new CreateIngestTaskResponse
        {
            TaskId = task.TaskId,
            ThreadId = task.ThreadId,
            AgentType = task.AgentType,
            Status = "Pending"
        };

        return Results.Created($"/api/tasks/{task.TaskId}", response);
    }

    /// <summary>
    /// Validates the ingest payload based on mode.
    /// </summary>
    private static IResult? ValidateIngestPayload(IngestPayload payload)
    {
        return payload.Mode switch
        {
            IngestMode.Url when string.IsNullOrWhiteSpace(payload.Url) =>
                Results.BadRequest(new
                {
                    code = "MISSING_URL",
                    message = "URL is required when mode is 'Url'",
                    mode = payload.Mode.ToString()
                }),
                
            IngestMode.Url when !IsValidUrl(payload.Url) =>
                Results.BadRequest(new
                {
                    code = "INVALID_URL",
                    message = "URL must be a valid HTTP or HTTPS URL",
                    url = payload.Url
                }),
                
            IngestMode.Query when string.IsNullOrWhiteSpace(payload.Query) =>
                Results.BadRequest(new
                {
                    code = "MISSING_QUERY",
                    message = "Query is required when mode is 'Query'",
                    mode = payload.Mode.ToString()
                }),
                
            IngestMode.Normalize when string.IsNullOrWhiteSpace(payload.RecipeId) =>
                Results.BadRequest(new
                {
                    code = "MISSING_RECIPE_ID",
                    message = "RecipeId is required when mode is 'Normalize'",
                    mode = payload.Mode.ToString()
                }),
                
            _ => null
        };
    }

    /// <summary>
    /// Validates that a URL is a proper HTTP/HTTPS URL.
    /// </summary>
    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) 
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Builds metadata dictionary from ingest payload for audit purposes.
    /// </summary>
    private static Dictionary<string, string> BuildIngestMetadata(IngestPayload payload, string? resolvedProviderId = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ingestMode"] = payload.Mode.ToString()
        };

        if (!string.IsNullOrWhiteSpace(payload.Url))
            metadata["sourceUrl"] = payload.Url;
            
        if (!string.IsNullOrWhiteSpace(payload.Query))
            metadata["searchQuery"] = payload.Query;
            
        if (!string.IsNullOrWhiteSpace(payload.RecipeId))
            metadata["recipeId"] = payload.RecipeId;

        // Record resolved search provider for query mode
        if (!string.IsNullOrWhiteSpace(resolvedProviderId))
            metadata["searchProviderId"] = resolvedProviderId;

        // Record prompt selections for audit
        if (payload.PromptSelection?.ExtractPromptId is not null)
            metadata["promptId:extract"] = payload.PromptSelection.ExtractPromptId;
            
        if (payload.PromptSelection?.NormalizePromptId is not null)
            metadata["promptId:normalize"] = payload.PromptSelection.NormalizePromptId;
            
        if (payload.PromptSelection?.DiscoverPromptId is not null)
            metadata["promptId:discover"] = payload.PromptSelection.DiscoverPromptId;

        return metadata;
    }

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
                code = "INVALID_AGENT_TYPE",
                message = $"Unknown agent type '{request.AgentType}'. Valid types are: {string.Join(", ", KnownAgentTypes.All)}",
                agentType = request.AgentType,
                validTypes = KnownAgentTypes.All
            });
        }

        // Normalize agent type to canonical casing
        var agentType = KnownAgentTypes.GetCanonical(request.AgentType) ?? request.AgentType;

        // Generate ThreadId if not provided
        var threadId = string.IsNullOrWhiteSpace(request.ThreadId) 
            ? Guid.NewGuid().ToString() 
            : request.ThreadId;

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
            ThreadId = threadId,
            AgentType = agentType,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };

        // Persist task to Cosmos
        await taskRepository.CreateAsync(task, cancellationToken);

        // Enqueue task via messaging bus
        await messagingBus.SendTaskAsync(task, cancellationToken);

        // Return response with generated ThreadId
        return Results.Created($"/api/tasks/{task.TaskId}", new
        {
            taskId = task.TaskId,
            threadId = task.ThreadId,
            agentType = task.AgentType,
            status = "Pending"
        });
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

    /// <summary>
    /// Rejects a task in ReviewReady state, transitioning it to the Rejected terminal state.
    /// </summary>
    /// <remarks>
    /// This endpoint handles:
    /// - Task state validation (must be ReviewReady)
    /// - Idempotency (returns 200 if already rejected)
    /// - Terminal state blocking (returns 400 if already committed/expired)
    /// </remarks>
    private static async Task<IResult> RejectTask(
        string taskId,
        RejectTaskRequest? request,
        ITaskRejectService rejectService,
        CancellationToken cancellationToken)
    {
        var result = await rejectService.RejectAsync(taskId, request?.Reason, cancellationToken);

        return result.StatusCode switch
        {
            200 => Results.Ok(result.Response),
            400 => Results.BadRequest(result.Error),
            404 => Results.NotFound(result.Error),
            _ => Results.Problem(
                statusCode: result.StatusCode,
                detail: result.Error?.Message ?? "An error occurred")
        };
    }

    /// <summary>
    /// Validates and resolves the search provider for query mode.
    /// Returns the resolved provider ID (defaulting if not specified).
    /// </summary>
    private static (string? ProviderId, IResult? Error) ValidateAndResolveSearchProvider(
        string? requestedProviderId,
        ISearchProviderResolver resolver)
    {
        // Default to the default provider if not specified
        var providerId = string.IsNullOrWhiteSpace(requestedProviderId) 
            ? resolver.DefaultProviderId 
            : requestedProviderId;

        // Try to resolve the provider
        if (!resolver.TryResolve(providerId, out _))
        {
            // Check if it's unknown or disabled
            var descriptor = resolver.GetDescriptor(providerId);
            
            if (descriptor is null)
            {
                return (null, Results.BadRequest(new
                {
                    code = "INVALID_SEARCH_PROVIDER",
                    message = $"Search provider '{providerId}' is not registered.",
                    providerId = providerId,
                    availableProviders = resolver.ListEnabled().Select(p => p.Id).ToList()
                }));
            }
            
            if (!descriptor.Enabled)
            {
                return (null, Results.BadRequest(new
                {
                    code = "INVALID_SEARCH_PROVIDER",
                    message = $"Search provider '{providerId}' is disabled.",
                    providerId = providerId,
                    availableProviders = resolver.ListEnabled().Select(p => p.Id).ToList()
                }));
            }
        }

        return (providerId, null);
    }
}
