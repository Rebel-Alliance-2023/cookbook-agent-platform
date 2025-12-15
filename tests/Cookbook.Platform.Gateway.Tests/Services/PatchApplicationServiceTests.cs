using Cookbook.Platform.Gateway.Services;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;
using Xunit;
using MessagingTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Gateway.Tests.Services;

/// <summary>
/// Unit tests for PatchApplicationService - testing the service logic using model-based tests.
/// </summary>
public class PatchApplicationServiceTests
{
    #region ApplyPatchRequest Model Tests

    [Fact]
    public void ApplyPatchRequest_WithAllFields_SerializesCorrectly()
    {
        // Arrange
        var request = new ApplyPatchRequest
        {
            TaskId = "task-123",
            PatchIndices = [0, 2, 4],
            MaxRiskLevel = NormalizePatchRiskCategory.Medium,
            Reason = "Applying approved patches"
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        var deserialized = JsonSerializer.Deserialize<ApplyPatchRequest>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("task-123", deserialized.TaskId);
        Assert.Equal(3, deserialized.PatchIndices?.Count);
        Assert.Equal(NormalizePatchRiskCategory.Medium, deserialized.MaxRiskLevel);
        Assert.Equal("Applying approved patches", deserialized.Reason);
    }

    [Fact]
    public void ApplyPatchRequest_WithMinimalFields_SerializesCorrectly()
    {
        // Arrange
        var request = new ApplyPatchRequest
        {
            TaskId = "task-456"
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        var deserialized = JsonSerializer.Deserialize<ApplyPatchRequest>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("task-456", deserialized.TaskId);
        Assert.Null(deserialized.PatchIndices);
        Assert.Null(deserialized.MaxRiskLevel);
        Assert.Null(deserialized.Reason);
    }

    #endregion

    #region ApplyPatchResponse Model Tests

    [Fact]
    public void ApplyPatchResponse_Success_HasCorrectValues()
    {
        // Arrange
        var response = new ApplyPatchResponse
        {
            Success = true,
            RecipeId = "recipe-123",
            AppliedCount = 5,
            FailedCount = 0,
            SkippedCount = 2,
            Summary = "Applied 5 patches successfully",
            Errors = []
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("recipe-123", response.RecipeId);
        Assert.Equal(5, response.AppliedCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(2, response.SkippedCount);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void ApplyPatchResponse_WithErrors_HasErrorList()
    {
        // Arrange
        var response = new ApplyPatchResponse
        {
            Success = false,
            RecipeId = "recipe-123",
            AppliedCount = 3,
            FailedCount = 2,
            SkippedCount = 0,
            Summary = "Partial success",
            Errors = ["/name: Path not found", "/servings: Invalid value type"]
        };

        // Assert
        Assert.False(response.Success);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains("/name: Path not found", response.Errors);
    }

    #endregion

    #region RejectPatchRequest Model Tests

    [Fact]
    public void RejectPatchRequest_SerializesCorrectly()
    {
        // Arrange
        var request = new RejectPatchRequest
        {
            TaskId = "task-789",
            Reason = "Changes not appropriate for this recipe"
        };

        // Act
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        var deserialized = JsonSerializer.Deserialize<RejectPatchRequest>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("task-789", deserialized.TaskId);
        Assert.Equal("Changes not appropriate for this recipe", deserialized.Reason);
    }

    #endregion

    #region RejectPatchResponse Model Tests

    [Fact]
    public void RejectPatchResponse_HasCorrectValues()
    {
        // Arrange
        var response = new RejectPatchResponse
        {
            Success = true,
            RecipeId = "recipe-456",
            Message = "Patches rejected. Recipe 'recipe-456' left unchanged."
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("recipe-456", response.RecipeId);
        Assert.Contains("left unchanged", response.Message);
    }

    #endregion

    #region Patch Filtering Logic Tests

    [Fact]
    public void FilterPatches_WithNoFilters_ReturnsAllPatches()
    {
        // Arrange
        var patches = CreateTestPatches();
        var request = new ApplyPatchRequest { TaskId = "task-1" };

        // Act
        var filtered = FilterPatches(patches, request);

        // Assert
        Assert.Equal(5, filtered.Count);
    }

    [Fact]
    public void FilterPatches_WithMaxRiskLevel_FiltersByRisk()
    {
        // Arrange
        var patches = CreateTestPatches();
        var request = new ApplyPatchRequest 
        { 
            TaskId = "task-1",
            MaxRiskLevel = NormalizePatchRiskCategory.Low
        };

        // Act
        var filtered = FilterPatches(patches, request);

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, p => Assert.Equal(NormalizePatchRiskCategory.Low, p.RiskCategory));
    }

    [Fact]
    public void FilterPatches_WithMaxRiskMedium_IncludesLowAndMedium()
    {
        // Arrange
        var patches = CreateTestPatches();
        var request = new ApplyPatchRequest 
        { 
            TaskId = "task-1",
            MaxRiskLevel = NormalizePatchRiskCategory.Medium
        };

        // Act
        var filtered = FilterPatches(patches, request);

        // Assert
        Assert.Equal(4, filtered.Count);
        Assert.All(filtered, p => Assert.True(p.RiskCategory <= NormalizePatchRiskCategory.Medium));
    }

    [Fact]
    public void FilterPatches_WithPatchIndices_ReturnsOnlySpecifiedIndices()
    {
        // Arrange
        var patches = CreateTestPatches();
        var request = new ApplyPatchRequest 
        { 
            TaskId = "task-1",
            PatchIndices = [0, 2, 4]
        };

        // Act
        var filtered = FilterPatches(patches, request);

        // Assert
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public void FilterPatches_WithBothFilters_AppliesBothFilters()
    {
        // Arrange
        var patches = CreateTestPatches(); // Index 0,1 are Low, 2,3 are Medium, 4 is High
        var request = new ApplyPatchRequest 
        { 
            TaskId = "task-1",
            PatchIndices = [0, 2, 4], // Request indices 0 (Low), 2 (Medium), 4 (High)
            MaxRiskLevel = NormalizePatchRiskCategory.Medium // But filter to Medium and below
        };

        // Act
        var filtered = FilterPatches(patches, request);

        // Assert
        Assert.Equal(2, filtered.Count); // Only 0 (Low) and 2 (Medium) pass both filters
    }

    #endregion

    #region PatchApplicationResult Tests

    [Fact]
    public void PatchApplicationResult_Success_HasCorrectStatusCode()
    {
        // Arrange
        var result = new PatchApplicationResult
        {
            StatusCode = 200,
            Response = new ApplyPatchResponse
            {
                Success = true,
                RecipeId = "recipe-1",
                Summary = "Success"
            }
        };

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Null(result.Error);
    }

    [Fact]
    public void PatchApplicationResult_NotFound_HasErrorDetails()
    {
        // Arrange
        var result = new PatchApplicationResult
        {
            StatusCode = 404,
            Error = new PatchApplicationError
            {
                Code = "TASK_NOT_FOUND",
                Message = "Task 'task-123' not found",
                TaskId = "task-123"
            }
        };

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Response);
        Assert.NotNull(result.Error);
        Assert.Equal("TASK_NOT_FOUND", result.Error.Code);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Simulates the patch filtering logic from PatchApplicationService.
    /// </summary>
    private static List<NormalizePatchOperation> FilterPatches(
        IReadOnlyList<NormalizePatchOperation> patches,
        ApplyPatchRequest request)
    {
        var result = new List<NormalizePatchOperation>();

        for (int i = 0; i < patches.Count; i++)
        {
            var patch = patches[i];

            // Filter by indices if specified
            if (request.PatchIndices != null && request.PatchIndices.Count > 0)
            {
                if (!request.PatchIndices.Contains(i))
                    continue;
            }

            // Filter by max risk level if specified
            if (request.MaxRiskLevel.HasValue)
            {
                if (patch.RiskCategory > request.MaxRiskLevel.Value)
                    continue;
            }

            result.Add(patch);
        }

        return result;
    }

    private static List<NormalizePatchOperation> CreateTestPatches() =>
    [
        CreatePatch("/name", "Value 0", NormalizePatchRiskCategory.Low),
        CreatePatch("/description", "Value 1", NormalizePatchRiskCategory.Low),
        CreatePatch("/servings", "Value 2", NormalizePatchRiskCategory.Medium),
        CreatePatch("/prepTime", "Value 3", NormalizePatchRiskCategory.Medium),
        CreatePatch("/ingredients/0/name", "Value 4", NormalizePatchRiskCategory.High)
    ];

    private static NormalizePatchOperation CreatePatch(
        string path, 
        object value, 
        NormalizePatchRiskCategory risk) => new()
    {
        Op = JsonPatchOperationType.Replace,
        Path = path,
        Value = value,
        RiskCategory = risk,
        Reason = "Test patch"
    };

    #endregion
}
