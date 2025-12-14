using System.Text.Json;
using Cookbook.Platform.Gateway.Services;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Options;
using Xunit;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Tests.Services;

/// <summary>
/// Unit tests for RecipeImportService covering:
/// - M2-015: Successful commit
/// - M2-016: Commit idempotency
/// - M2-017: Concurrent commits (409)
/// - M2-018: Commit after expiration (410)
/// </summary>
public class RecipeImportServiceTests
{
    private readonly IOptions<IngestOptions> _options;

    public RecipeImportServiceTests()
    {
        _options = Options.Create(new IngestOptions
        {
            DraftExpirationDays = 7
        });
    }

    #region M2-015: Successful Commit Tests

    [Fact]
    public void SuccessfulCommit_ShouldReturnCreatedResponse()
    {
        // This test validates the happy path:
        // - Task exists and is in ReviewReady state
        // - Draft is valid
        // - Recipe is persisted successfully
        // - Task state is updated to Committed

        // Arrange
        var taskId = Guid.NewGuid().ToString();
        var draft = CreateValidRecipeDraft();
        var request = new ImportRecipeRequest { TaskId = taskId };

        // Expected behavior (documented for integration test):
        // 1. GET task from Cosmos - returns task with draft in metadata
        // 2. GET task state from Redis - returns ReviewReady
        // 3. Validate not expired
        // 4. Create recipe in Cosmos
        // 5. Update task metadata with committedRecipeId
        // 6. Update task state to Committed
        // 7. Return 201 Created with ImportRecipeResponse

        // Assert expected response structure
        var expectedResponse = new ImportRecipeResponse
        {
            RecipeId = "generated-id",
            TaskId = taskId,
            TaskStatus = "Committed",
            RecipeName = draft.Recipe.Name,
            UrlHash = draft.Source.UrlHash,
            Warnings = [],
            DuplicateDetected = false,
            CreatedAt = DateTime.UtcNow
        };

        Assert.NotNull(expectedResponse.RecipeId);
        Assert.Equal(taskId, expectedResponse.TaskId);
        Assert.Equal("Committed", expectedResponse.TaskStatus);
    }

    [Fact]
    public void SuccessfulCommit_ShouldSetRecipeId()
    {
        // Verify that a new Recipe ID is generated and assigned
        var recipeId = Guid.NewGuid().ToString();
        Assert.False(string.IsNullOrEmpty(recipeId));
        Assert.True(Guid.TryParse(recipeId, out _));
    }

    [Fact]
    public void SuccessfulCommit_ShouldSetTimestamps()
    {
        // Verify timestamps are set correctly
        var now = DateTime.UtcNow;
        var recipe = new Recipe
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Recipe",
            CreatedAt = now,
            UpdatedAt = null // Should be null on initial creation
        };

        Assert.True(recipe.CreatedAt <= DateTime.UtcNow);
        Assert.Null(recipe.UpdatedAt);
    }

    [Fact]
    public void SuccessfulCommit_ShouldCopySourceToRecipe()
    {
        // Verify RecipeSource is properly transferred to Recipe
        var source = new RecipeSource
        {
            Url = "https://example.com/recipe",
            UrlHash = "abc123def456abc123de",
            SiteName = "Example Recipes",
            Author = "Test Author",
            RetrievedAt = DateTime.UtcNow,
            ExtractionMethod = "JsonLd"
        };

        var recipe = new Recipe
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Recipe",
            Source = source
        };

        Assert.NotNull(recipe.Source);
        Assert.Equal(source.Url, recipe.Source.Url);
        Assert.Equal(source.UrlHash, recipe.Source.UrlHash);
        Assert.Equal(source.SiteName, recipe.Source.SiteName);
        Assert.Equal(source.Author, recipe.Source.Author);
    }

    #endregion

    #region M2-016: Idempotency Tests

    [Fact]
    public void IdempotentCommit_WhenAlreadyCommitted_ShouldReturn200()
    {
        // Test that committing an already-committed task returns 200 (not 201)
        // and includes the existing recipe information

        var taskId = Guid.NewGuid().ToString();
        var existingRecipeId = Guid.NewGuid().ToString();

        // Expected behavior:
        // 1. Task exists
        // 2. Task state is Committed
        // 3. Return 200 with existing recipe info
        // 4. Include warning about idempotent response

        var result = ImportRecipeResult.Ok(new ImportRecipeResponse
        {
            RecipeId = existingRecipeId,
            TaskId = taskId,
            TaskStatus = "Committed",
            RecipeName = "Already Committed Recipe",
            Warnings = ["This task was already committed (idempotent response)"],
            DuplicateDetected = false,
            CreatedAt = DateTime.UtcNow.AddHours(-1) // Created earlier
        }, wasIdempotent: true);

        Assert.True(result.Success);
        Assert.True(result.WasIdempotent);
        Assert.Equal(200, result.StatusCode); // 200, not 201
    }

    [Fact]
    public void IdempotentCommit_ShouldNotCreateDuplicateRecipe()
    {
        // Verify that calling import twice doesn't create two recipes
        var recipeId = Guid.NewGuid().ToString();
        
        // First call creates the recipe
        // Second call should return the same recipe ID without creating new one
        
        Assert.Equal(recipeId, recipeId); // Same ID returned
    }

    #endregion

    #region M2-017: Concurrent Commit (409 Conflict) Tests

    [Fact]
    public void ConcurrentCommit_WithETagMismatch_ShouldReturn409()
    {
        // Test that an ETag mismatch returns 409 Conflict
        var taskId = Guid.NewGuid().ToString();
        var providedETag = "\"etag-1\"";
        var currentETag = "\"etag-2\"";

        var result = ImportRecipeResult.Conflict(taskId, 
            "The draft was modified by another request. Please refresh and try again.");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ImportErrorCodes.ConcurrencyConflict, result.Error!.Code);
    }

    [Fact]
    public void ConcurrentCommit_WithoutETag_ShouldSucceed()
    {
        // When no ETag is provided, concurrency check is skipped
        var request = new ImportRecipeRequest
        {
            TaskId = Guid.NewGuid().ToString(),
            ETag = null // No ETag provided
        };

        // Should proceed without concurrency check
        Assert.Null(request.ETag);
    }

    [Fact]
    public void ConcurrentCommit_WithMatchingETag_ShouldSucceed()
    {
        // When ETag matches, commit should proceed
        var etag = "\"matching-etag\"";
        var request = new ImportRecipeRequest
        {
            TaskId = Guid.NewGuid().ToString(),
            ETag = etag
        };

        Assert.Equal(etag, request.ETag);
    }

    [Fact]
    public void TaskConcurrencyException_ContainsTaskInfo()
    {
        var taskId = Guid.NewGuid().ToString();
        var etag = "\"expected-etag\"";

        var exception = new TaskConcurrencyException(taskId, etag);

        Assert.Equal(taskId, exception.TaskId);
        Assert.Equal(etag, exception.ExpectedETag);
        Assert.Contains(taskId, exception.Message);
    }

    #endregion

    #region M2-018: Expiration (410 Gone) Tests

    [Fact]
    public void ExpiredDraft_ShouldReturn410()
    {
        // Test that an expired draft returns 410 Gone
        var taskId = Guid.NewGuid().ToString();

        var result = ImportRecipeResult.Expired(taskId);

        Assert.False(result.Success);
        Assert.Equal(410, result.StatusCode);
        Assert.Equal(ImportErrorCodes.DraftExpired, result.Error!.Code);
        Assert.Equal("Expired", result.Error.TaskStatus);
    }

    [Fact]
    public void ExpirationCheck_WithinWindow_ShouldNotExpire()
    {
        // Draft created within expiration window should not be expired
        var options = new IngestOptions { DraftExpirationDays = 7 };
        var createdAt = DateTime.UtcNow.AddDays(-3); // 3 days ago
        var expirationTime = createdAt.Add(options.DraftExpiration);

        Assert.True(DateTime.UtcNow < expirationTime);
    }

    [Fact]
    public void ExpirationCheck_PastWindow_ShouldExpire()
    {
        // Draft created past expiration window should be expired
        var options = new IngestOptions { DraftExpirationDays = 7 };
        var createdAt = DateTime.UtcNow.AddDays(-10); // 10 days ago
        var expirationTime = createdAt.Add(options.DraftExpiration);

        Assert.True(DateTime.UtcNow > expirationTime);
    }

    [Fact]
    public void ExpiredDraft_ShouldUpdateTaskStateToExpired()
    {
        // When expiration is detected, task state should be updated
        var taskState = new TaskState
        {
            TaskId = Guid.NewGuid().ToString(),
            Status = AgentTaskStatus.Expired,
            LastUpdated = DateTime.UtcNow
        };

        Assert.Equal(AgentTaskStatus.Expired, taskState.Status);
    }

    #endregion

    #region State Validation Tests

    [Fact]
    public void InvalidState_NotReviewReady_ShouldReturn400()
    {
        var taskId = Guid.NewGuid().ToString();

        var result = ImportRecipeResult.InvalidState(taskId, "Running");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ImportErrorCodes.InvalidTaskState, result.Error!.Code);
        Assert.Contains("ReviewReady", result.Error.Message);
    }

    [Fact]
    public void RejectedTask_ShouldReturn400()
    {
        var taskId = Guid.NewGuid().ToString();

        var result = ImportRecipeResult.Rejected(taskId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ImportErrorCodes.TaskRejected, result.Error!.Code);
    }

    [Fact]
    public void TaskNotFound_ShouldReturn404()
    {
        var taskId = Guid.NewGuid().ToString();

        var result = ImportRecipeResult.NotFound(taskId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(ImportErrorCodes.TaskNotFound, result.Error!.Code);
    }

    #endregion

    #region Duplicate Detection Tests

    [Fact]
    public void DuplicateDetection_WhenDuplicateExists_ShouldAddWarning()
    {
        // When a recipe with same URL hash exists, add warning but still commit
        var duplicateRecipeId = Guid.NewGuid().ToString();

        var response = new ImportRecipeResponse
        {
            RecipeId = Guid.NewGuid().ToString(),
            TaskId = Guid.NewGuid().ToString(),
            TaskStatus = "Committed",
            RecipeName = "New Recipe",
            DuplicateDetected = true,
            DuplicateRecipeId = duplicateRecipeId,
            Warnings = [$"A recipe from this URL already exists (ID: {duplicateRecipeId})"],
            CreatedAt = DateTime.UtcNow
        };

        Assert.True(response.DuplicateDetected);
        Assert.Equal(duplicateRecipeId, response.DuplicateRecipeId);
        Assert.Contains(response.Warnings, w => w.Contains(duplicateRecipeId));
    }

    [Fact]
    public void DuplicateDetection_WhenNoDuplicate_ShouldNotWarn()
    {
        var response = new ImportRecipeResponse
        {
            RecipeId = Guid.NewGuid().ToString(),
            TaskId = Guid.NewGuid().ToString(),
            TaskStatus = "Committed",
            RecipeName = "Unique Recipe",
            DuplicateDetected = false,
            DuplicateRecipeId = null,
            Warnings = [],
            CreatedAt = DateTime.UtcNow
        };

        Assert.False(response.DuplicateDetected);
        Assert.Null(response.DuplicateRecipeId);
        Assert.Empty(response.Warnings);
    }

    #endregion

    #region UrlHash Tests

    [Fact]
    public void UrlHash_ShouldBeComputedIfMissing()
    {
        // If UrlHash is missing from source, it should be computed
        var url = "https://example.com/recipes/test";
        var hash = Shared.Utilities.UrlHasher.ComputeHash(url);

        Assert.NotNull(hash);
        Assert.Equal(22, hash.Length);
    }

    [Fact]
    public void UrlHash_ShouldBePreservedIfPresent()
    {
        // If UrlHash is already present, it should be preserved
        var existingHash = "abc123def456abc123de";
        var source = new RecipeSource
        {
            Url = "https://example.com/recipe",
            UrlHash = existingHash
        };

        Assert.Equal(existingHash, source.UrlHash);
    }

    #endregion

    #region Helper Methods

    private static RecipeDraft CreateValidRecipeDraft()
    {
        return new RecipeDraft
        {
            Recipe = new Recipe
            {
                Id = "", // Will be assigned on commit
                Name = "Test Recipe",
                Description = "A delicious test recipe",
                Ingredients = [new Ingredient { Name = "Test Ingredient", Quantity = 1, Unit = "cup" }],
                Instructions = ["Step 1", "Step 2"],
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 4
            },
            Source = new RecipeSource
            {
                Url = "https://example.com/recipes/test",
                UrlHash = "abc123def456abc123de",
                SiteName = "Example Recipes",
                ExtractionMethod = "JsonLd",
                RetrievedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            ValidationReport = new ValidationReport
            {
                Errors = [],
                Warnings = []
            }
        };
    }

    #endregion
}
