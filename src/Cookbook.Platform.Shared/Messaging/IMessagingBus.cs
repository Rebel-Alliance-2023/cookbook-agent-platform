using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Messaging;

/// <summary>
/// Abstraction for messaging bus operations (sending tasks, publishing events, subscribing, cancellation).
/// </summary>
public interface IMessagingBus
{
    /// <summary>
    /// Sends a task to be processed by an agent.
    /// </summary>
    Task SendTaskAsync(AgentTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to a specific thread/group.
    /// </summary>
    Task PublishEventAsync(string threadId, AgentEvent agentEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to events for a specific thread.
    /// </summary>
    IAsyncEnumerable<AgentEvent> SubscribeAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running task.
    /// </summary>
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a task.
    /// </summary>
    Task<TaskState?> GetTaskStateAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state of a task with TTL support.
    /// </summary>
    Task SetTaskStateAsync(string taskId, TaskState state, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a task to be processed by an agent.
/// </summary>
public record AgentTask
{
    /// <summary>
    /// Cosmos DB document ID (maps to taskId).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id => TaskId;
    
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("agentType")]
    [JsonProperty("agentType")]
    public required string AgentType { get; init; }
    
    [JsonPropertyName("payload")]
    [JsonProperty("payload")]
    public required string Payload { get; init; }
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Represents an event published by an agent.
/// </summary>
public record AgentEvent
{
    [JsonPropertyName("eventId")]
    [JsonProperty("eventId")]
    public required string EventId { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("eventType")]
    [JsonProperty("eventType")]
    public required string EventType { get; init; }
    
    [JsonPropertyName("payload")]
    [JsonProperty("payload")]
    public required string Payload { get; init; }
    
    [JsonPropertyName("timestamp")]
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Represents the state of a task.
/// </summary>
public record TaskState
{
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }
    
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public required TaskStatus Status { get; init; }
    
    [JsonPropertyName("result")]
    [JsonProperty("result")]
    public string? Result { get; init; }
    
    [JsonPropertyName("error")]
    [JsonProperty("error")]
    public string? Error { get; init; }
    
    [JsonPropertyName("progress")]
    [JsonProperty("progress")]
    public int Progress { get; init; }
    
    [JsonPropertyName("currentPhase")]
    [JsonProperty("currentPhase")]
    public string? CurrentPhase { get; init; }
    
    [JsonPropertyName("lastUpdated")]
    [JsonProperty("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Task status enumeration.
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    /// <summary>
    /// Task has completed extraction and is awaiting human review.
    /// </summary>
    ReviewReady,
    /// <summary>
    /// Draft was committed and converted to a canonical Recipe.
    /// </summary>
    Committed,
    /// <summary>
    /// Draft was explicitly rejected by user.
    /// </summary>
    Rejected,
    /// <summary>
    /// Draft expired without being committed or rejected.
    /// </summary>
    Expired
}
