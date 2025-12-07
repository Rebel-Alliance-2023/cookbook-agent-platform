using Cookbook.Platform.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Storage.Repositories;

/// <summary>
/// Repository for Session entities in Cosmos DB.
/// </summary>
public class SessionRepository
{
    private readonly Container _container;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<SessionRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "sessions");
        _logger = logger;
    }

    public async Task<Session?> GetByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.ThreadId = @threadId")
                .WithParameter("@threadId", threadId);

            using var iterator = _container.GetItemQueryIterator<Session>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }
            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Session> CreateAsync(Session session, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(session, new PartitionKey(session.ThreadId), cancellationToken: cancellationToken);
        _logger.LogInformation("Created session {SessionId} for thread {ThreadId}", session.Id, session.ThreadId);
        return response.Resource;
    }

    public async Task<Session> UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        var updatedSession = session with { LastActivityAt = DateTime.UtcNow };
        var response = await _container.ReplaceItemAsync(updatedSession, session.Id, new PartitionKey(session.ThreadId), cancellationToken: cancellationToken);
        return response.Resource;
    }
}

/// <summary>
/// Repository for Message entities in Cosmos DB.
/// </summary>
public class MessageRepository
{
    private readonly Container _container;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<MessageRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "messages");
        _logger = logger;
    }

    public async Task<List<Message>> GetByThreadIdAsync(string threadId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT TOP @limit * FROM c WHERE c.ThreadId = @threadId ORDER BY c.Timestamp DESC")
            .WithParameter("@threadId", threadId)
            .WithParameter("@limit", limit);

        var results = new List<Message>();
        using var iterator = _container.GetItemQueryIterator<Message>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results.OrderBy(m => m.Timestamp).ToList();
    }

    public async Task<Message> CreateAsync(Message message, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(message, new PartitionKey(message.ThreadId), cancellationToken: cancellationToken);
        _logger.LogDebug("Created message {MessageId} in thread {ThreadId}", message.Id, message.ThreadId);
        return response.Resource;
    }
}
