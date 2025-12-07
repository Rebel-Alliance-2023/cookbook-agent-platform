using System.Runtime.CompilerServices;
using System.Text.Json;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cookbook.Platform.Infrastructure.Messaging;

/// <summary>
/// Redis + SignalR implementation of IMessagingBus.
/// Uses Redis Streams for task ingestion and Redis Keys with TTL for semi-durable state.
/// </summary>
public class RedisSignalRMessagingBus : IMessagingBus
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly MessagingBusOptions _options;
    private readonly ILogger<RedisSignalRMessagingBus> _logger;

    public RedisSignalRMessagingBus(
        IConnectionMultiplexer redis,
        IHubContext<AgentHub> hubContext,
        IOptions<MessagingBusOptions> options,
        ILogger<RedisSignalRMessagingBus> logger)
    {
        _redis = redis;
        _hubContext = hubContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase(_options.RedisDb);
        var streamKey = $"{_options.RedisStreamPrefix}{task.AgentType}";
        var payload = JsonSerializer.Serialize(task);

        await db.StreamAddAsync(streamKey, "task", payload);
        _logger.LogInformation("Sent task {TaskId} to stream {StreamKey}", task.TaskId, streamKey);

        // Set initial task state
        await SetTaskStateAsync(task.TaskId, new TaskState
        {
            TaskId = task.TaskId,
            Status = Shared.Messaging.TaskStatus.Pending,
            CurrentPhase = "Queued"
        }, TimeSpan.FromMinutes(_options.DefaultTtlMinutes), cancellationToken);
    }

    public async Task PublishEventAsync(string threadId, AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(threadId).SendAsync("ReceiveEvent", agentEvent, cancellationToken);
        _logger.LogDebug("Published event {EventId} to thread {ThreadId}", agentEvent.EventId, threadId);
    }

    public async IAsyncEnumerable<AgentEvent> SubscribeAsync(string threadId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase(_options.RedisDb);
        var streamKey = $"{_options.RedisStreamPrefix}events:{threadId}";
        var lastId = "0-0";

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await db.StreamReadAsync(streamKey, lastId, count: 10);
            
            foreach (var entry in entries)
            {
                lastId = entry.Id!;
                var payload = entry.Values.FirstOrDefault(v => v.Name == "event").Value;
                
                if (!payload.IsNullOrEmpty)
                {
                    var agentEvent = JsonSerializer.Deserialize<AgentEvent>((string)payload!);
                    if (agentEvent != null)
                    {
                        yield return agentEvent;
                    }
                }
            }

            if (entries.Length == 0)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var currentState = await GetTaskStateAsync(taskId, cancellationToken);
        if (currentState != null && currentState.Status == Shared.Messaging.TaskStatus.Running)
        {
            await SetTaskStateAsync(taskId, currentState with 
            { 
                Status = Shared.Messaging.TaskStatus.Cancelled,
                LastUpdated = DateTime.UtcNow
            }, null, cancellationToken);
            
            _logger.LogInformation("Cancelled task {TaskId}", taskId);
        }
    }

    public async Task<TaskState?> GetTaskStateAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase(_options.RedisDb);
        var key = $"{_options.RedisKeyPrefix}{taskId}";
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TaskState>((string)value!);
    }

    public async Task SetTaskStateAsync(string taskId, TaskState state, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase(_options.RedisDb);
        var key = $"{_options.RedisKeyPrefix}{taskId}";
        var payload = JsonSerializer.Serialize(state);
        var expiry = ttl ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes);

        await db.StringSetAsync(key, payload, expiry);
        _logger.LogDebug("Updated task state for {TaskId}: {Status}", taskId, state.Status);
    }
}
