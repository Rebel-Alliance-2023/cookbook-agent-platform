using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Cookbook.Platform.Storage.Repositories;

/// <summary>
/// Repository for Task entities in Cosmos DB.
/// </summary>
public class TaskRepository
{
    private readonly Container _container;
    private readonly ILogger<TaskRepository> _logger;

    public TaskRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<TaskRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "tasks");
        _logger = logger;
    }

    public async Task<AgentTask?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<AgentTask>(taskId, new PartitionKey(taskId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a task by ID with its ETag for optimistic concurrency.
    /// </summary>
    public async Task<(AgentTask? Task, string? ETag)> GetByIdWithETagAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<AgentTask>(taskId, new PartitionKey(taskId), cancellationToken: cancellationToken);
            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }
    }

    public async Task<List<AgentTask>> GetByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.ThreadId = @threadId ORDER BY c.CreatedAt DESC")
            .WithParameter("@threadId", threadId);

        var results = new List<AgentTask>();
        using var iterator = _container.GetItemQueryIterator<AgentTask>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<AgentTask> CreateAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(task, new PartitionKey(task.TaskId), cancellationToken: cancellationToken);
        _logger.LogInformation("Created task {TaskId} for agent {AgentType}", task.TaskId, task.AgentType);
        return response.Resource;
    }

    /// <summary>
    /// Updates a task with optimistic concurrency using ETag.
    /// </summary>
    /// <param name="task">The updated task.</param>
    /// <param name="etag">The ETag from the original read. If null, no concurrency check is performed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated task and new ETag.</returns>
    /// <exception cref="TaskConcurrencyException">Thrown when ETag doesn't match (409 Conflict).</exception>
    public async Task<(AgentTask Task, string ETag)> UpdateAsync(AgentTask task, string? etag = null, CancellationToken cancellationToken = default)
    {
        var requestOptions = new ItemRequestOptions();
        
        if (!string.IsNullOrEmpty(etag))
        {
            requestOptions.IfMatchEtag = etag;
        }

        try
        {
            var response = await _container.ReplaceItemAsync(
                task, 
                task.TaskId, 
                new PartitionKey(task.TaskId), 
                requestOptions, 
                cancellationToken);
            
            _logger.LogInformation("Updated task {TaskId}, new ETag: {ETag}", task.TaskId, response.ETag);
            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning("Concurrency conflict updating task {TaskId}. ETag mismatch.", task.TaskId);
            throw new TaskConcurrencyException(task.TaskId, etag ?? "unknown");
        }
    }

    /// <summary>
    /// Updates specific metadata fields on a task.
    /// </summary>
    public async Task<AgentTask> UpdateMetadataAsync(string taskId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        var (task, etag) = await GetByIdWithETagAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"Task {taskId} not found");
        }

        var updatedMetadata = new Dictionary<string, string>(task.Metadata);
        foreach (var kvp in metadata)
        {
            updatedMetadata[kvp.Key] = kvp.Value;
        }

        var updatedTask = task with { Metadata = updatedMetadata };
        var (result, _) = await UpdateAsync(updatedTask, etag, cancellationToken);
        return result;
    }
}

/// <summary>
/// Exception thrown when a task update fails due to ETag mismatch.
/// </summary>
public class TaskConcurrencyException : Exception
{
    public string TaskId { get; }
    public string ExpectedETag { get; }

    public TaskConcurrencyException(string taskId, string expectedETag)
        : base($"Concurrency conflict updating task {taskId}. The task was modified by another request.")
    {
        TaskId = taskId;
        ExpectedETag = expectedETag;
    }
}

/// <summary>
/// Repository for Artifact entities in Cosmos DB.
/// </summary>
public class ArtifactRepository
{
    private readonly Container _container;
    private readonly ILogger<ArtifactRepository> _logger;

    public ArtifactRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<ArtifactRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "artifacts");
        _logger = logger;
    }

    public async Task<List<Artifact>> GetByTaskIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.TaskId = @taskId ORDER BY c.CreatedAt DESC")
            .WithParameter("@taskId", taskId);

        var results = new List<Artifact>();
        using var iterator = _container.GetItemQueryIterator<Artifact>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Artifact> CreateAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(artifact, new PartitionKey(artifact.TaskId), cancellationToken: cancellationToken);
        _logger.LogInformation("Created artifact {ArtifactId} for task {TaskId}", artifact.Id, artifact.TaskId);
        return response.Resource;
    }
}

/// <summary>
/// Repository for Notes entities in Cosmos DB.
/// </summary>
public class NotesRepository
{
    private readonly Container _container;
    private readonly ILogger<NotesRepository> _logger;

    public NotesRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<NotesRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "notes");
        _logger = logger;
    }

    public async Task<List<Notes>> GetByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.ThreadId = @threadId ORDER BY c.CreatedAt DESC")
            .WithParameter("@threadId", threadId);

        var results = new List<Notes>();
        using var iterator = _container.GetItemQueryIterator<Notes>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Notes> CreateAsync(Notes notes, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(notes, new PartitionKey(notes.ThreadId), cancellationToken: cancellationToken);
        _logger.LogInformation("Created notes {NotesId} for thread {ThreadId}", notes.Id, notes.ThreadId);
        return response.Resource;
    }

    public async Task<Notes> UpdateAsync(Notes notes, CancellationToken cancellationToken = default)
    {
        var updatedNotes = notes with { UpdatedAt = DateTime.UtcNow };
        var response = await _container.ReplaceItemAsync(updatedNotes, notes.Id, new PartitionKey(notes.ThreadId), cancellationToken: cancellationToken);
        return response.Resource;
    }
}
