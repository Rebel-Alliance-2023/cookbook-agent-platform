using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models.Ingest;

/// <summary>
/// Request DTO for importing (committing) a recipe draft to the permanent collection.
/// </summary>
public record ImportRecipeRequest
{
    /// <summary>
    /// The ID of the ingest task containing the draft to commit.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// Optional ETag for optimistic concurrency control.
    /// If provided, the commit will fail with 409 if the draft has been modified.
    /// </summary>
    [JsonPropertyName("etag")]
    [JsonProperty("etag")]
    public string? ETag { get; init; }

    /// <summary>
    /// Optional override values to apply to the recipe before committing.
    /// </summary>
    [JsonPropertyName("overrides")]
    [JsonProperty("overrides")]
    public RecipeOverrides? Overrides { get; init; }
}

/// <summary>
/// Optional overrides to apply to a recipe during import.
/// </summary>
public record RecipeOverrides
{
    /// <summary>
    /// Override the recipe name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Override the recipe description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Override the cuisine type.
    /// </summary>
    [JsonPropertyName("cuisine")]
    [JsonProperty("cuisine")]
    public string? Cuisine { get; init; }

    /// <summary>
    /// Override the diet type.
    /// </summary>
    [JsonPropertyName("dietType")]
    [JsonProperty("dietType")]
    public string? DietType { get; init; }

    /// <summary>
    /// Override or add tags.
    /// </summary>
    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public List<string>? Tags { get; init; }
}

/// <summary>
/// Response DTO for a successful recipe import.
/// </summary>
public record ImportRecipeResponse
{
    /// <summary>
    /// The ID of the newly created recipe.
    /// </summary>
    [JsonPropertyName("recipeId")]
    [JsonProperty("recipeId")]
    public required string RecipeId { get; init; }

    /// <summary>
    /// The ID of the source ingest task.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// The new status of the task (should be "Committed").
    /// </summary>
    [JsonPropertyName("taskStatus")]
    [JsonProperty("taskStatus")]
    public required string TaskStatus { get; init; }

    /// <summary>
    /// The name of the imported recipe.
    /// </summary>
    [JsonPropertyName("recipeName")]
    [JsonProperty("recipeName")]
    public required string RecipeName { get; init; }

    /// <summary>
    /// URL hash for duplicate detection reference.
    /// </summary>
    [JsonPropertyName("urlHash")]
    [JsonProperty("urlHash")]
    public string? UrlHash { get; init; }

    /// <summary>
    /// Warnings generated during the import process.
    /// </summary>
    [JsonPropertyName("warnings")]
    [JsonProperty("warnings")]
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Indicates if a duplicate recipe was detected by URL hash.
    /// </summary>
    [JsonPropertyName("duplicateDetected")]
    [JsonProperty("duplicateDetected")]
    public bool DuplicateDetected { get; init; }

    /// <summary>
    /// The ID of the existing duplicate recipe, if any.
    /// </summary>
    [JsonPropertyName("duplicateRecipeId")]
    [JsonProperty("duplicateRecipeId")]
    public string? DuplicateRecipeId { get; init; }

    /// <summary>
    /// Timestamp when the recipe was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Standard error response for import failures.
/// </summary>
public record ImportRecipeError
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonProperty("message")]
    public required string Message { get; init; }

    /// <summary>
    /// The task ID that was attempted.
    /// </summary>
    [JsonPropertyName("taskId")]
    [JsonProperty("taskId")]
    public string? TaskId { get; init; }

    /// <summary>
    /// Current task status if available.
    /// </summary>
    [JsonPropertyName("taskStatus")]
    [JsonProperty("taskStatus")]
    public string? TaskStatus { get; init; }

    /// <summary>
    /// Additional details about the error.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonProperty("details")]
    public Dictionary<string, object>? Details { get; init; }
}

/// <summary>
/// Well-known error codes for import operations.
/// </summary>
public static class ImportErrorCodes
{
    /// <summary>Task not found.</summary>
    public const string TaskNotFound = "TASK_NOT_FOUND";
    
    /// <summary>Task is not in ReviewReady state.</summary>
    public const string InvalidTaskState = "INVALID_TASK_STATE";
    
    /// <summary>Draft has expired and can no longer be committed.</summary>
    public const string DraftExpired = "DRAFT_EXPIRED";
    
    /// <summary>ETag mismatch - draft was modified by another request.</summary>
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    
    /// <summary>Task was already committed (idempotent success).</summary>
    public const string AlreadyCommitted = "ALREADY_COMMITTED";
    
    /// <summary>Task was rejected and cannot be committed.</summary>
    public const string TaskRejected = "TASK_REJECTED";
    
    /// <summary>Draft data is missing or corrupted.</summary>
    public const string InvalidDraft = "INVALID_DRAFT";
    
    /// <summary>Failed to persist the recipe.</summary>
    public const string PersistenceFailed = "PERSISTENCE_FAILED";
}
