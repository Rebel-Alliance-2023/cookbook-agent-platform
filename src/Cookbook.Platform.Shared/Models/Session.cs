using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models;

/// <summary>
/// Represents a user session in the system.
/// </summary>
public record Session
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("userId")]
    [JsonProperty("userId")]
    public string? UserId { get; init; }
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("lastActivityAt")]
    [JsonProperty("lastActivityAt")]
    public DateTime? LastActivityAt { get; init; }
    
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public SessionStatus Status { get; init; } = SessionStatus.Active;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Session status enumeration.
/// </summary>
public enum SessionStatus
{
    Active,
    Completed,
    Abandoned
}

/// <summary>
/// Represents a message in a conversation thread.
/// </summary>
public record Message
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("role")]
    [JsonProperty("role")]
    public required string Role { get; init; } // user, assistant, system
    
    [JsonPropertyName("content")]
    [JsonProperty("content")]
    public required string Content { get; init; }
    
    [JsonPropertyName("timestamp")]
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Represents notes saved during research.
/// </summary>
public record Notes
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("content")]
    [JsonProperty("content")]
    public required string Content { get; init; }
    
    [JsonPropertyName("recipeId")]
    [JsonProperty("recipeId")]
    public string? RecipeId { get; init; }
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime? UpdatedAt { get; init; }
}
