using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models;

/// <summary>
/// Represents an artifact generated during analysis.
/// </summary>
public record Artifact
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }
    
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public required ArtifactType Type { get; init; }
    
    [JsonPropertyName("contentType")]
    [JsonProperty("contentType")]
    public string? ContentType { get; init; }
    
    [JsonPropertyName("sizeBytes")]
    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; init; }
    
    [JsonPropertyName("blobUri")]
    [JsonProperty("blobUri")]
    public string? BlobUri { get; init; }
    
    [JsonPropertyName("inlineContent")]
    [JsonProperty("inlineContent")]
    public string? InlineContent { get; init; }
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// Artifact type enumeration.
/// </summary>
public enum ArtifactType
{
    Markdown,
    Pdf,
    Image,
    Json,
    Text
}

/// <summary>
/// Represents a shopping list generated from a recipe.
/// </summary>
public record ShoppingList
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("recipeId")]
    [JsonProperty("recipeId")]
    public required string RecipeId { get; init; }
    
    [JsonPropertyName("threadId")]
    [JsonProperty("threadId")]
    public required string ThreadId { get; init; }
    
    [JsonPropertyName("items")]
    [JsonProperty("items")]
    public List<ShoppingItem> Items { get; init; } = [];
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an item in a shopping list.
/// </summary>
public record ShoppingItem
{
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public double Quantity { get; init; }
    
    [JsonPropertyName("unit")]
    [JsonProperty("unit")]
    public string? Unit { get; init; }
    
    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public string? Category { get; init; }
    
    [JsonPropertyName("isPurchased")]
    [JsonProperty("isPurchased")]
    public bool IsPurchased { get; init; }
}
