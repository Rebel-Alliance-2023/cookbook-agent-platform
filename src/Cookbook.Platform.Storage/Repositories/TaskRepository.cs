using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

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
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
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
