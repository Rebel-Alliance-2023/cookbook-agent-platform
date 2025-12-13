using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Response DTO for ingest task creation.
/// </summary>
public record CreateIngestTaskResponse
{
    /// <summary>
    /// The unique identifier for the created task.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }
    
    /// <summary>
    /// The thread ID for the task (may be generated if not provided).
    /// </summary>
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    /// <summary>
    /// The agent type handling this task.
    /// </summary>
    [JsonPropertyName("agentType")]
    [JsonProperty("agentType")]
    public required string AgentType { get; init; }
    
    /// <summary>
    /// The initial status of the task.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public required string Status { get; init; }
}

/// <summary>
/// Request DTO for creating an ingest task.
/// </summary>
public record CreateIngestTaskRequest
{
    /// <summary>
    /// The agent type (should be "Ingest").
    /// </summary>
    [JsonPropertyName("agentType")]
    [JsonProperty("agentType")]
    public required string AgentType { get; init; }
    
    /// <summary>
    /// Optional thread ID. If not provided, one will be generated.
    /// </summary>
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public string? ThreadId { get; init; }
    
    /// <summary>
    /// The ingest payload containing mode-specific parameters.
    /// </summary>
    [JsonPropertyName("payload")]
    [JsonProperty("payload")]
    public required IngestPayload Payload { get; init; }
}
