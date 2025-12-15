using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Prompts;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for NormalizeService patch validation and application.
/// </summary>
public class NormalizeServiceTests
{
    private readonly Mock<ILlmRouter> _llmRouterMock;
    private readonly Mock<IPromptRenderer> _promptRendererMock;
    private readonly Mock<ILogger<NormalizeService>> _loggerMock;
    private readonly NormalizeService _service;

    public NormalizeServiceTests()
    {
        _llmRouterMock = new Mock<ILlmRouter>();
        _promptRendererMock = new Mock<IPromptRenderer>();
        _loggerMock = new Mock<ILogger<NormalizeService>>();
        
        _promptRendererMock
            .Setup(r => r.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>()))
            .Returns("rendered prompt");

        _service = new NormalizeService(
            _llmRouterMock.Object,
            _promptRendererMock.Object,
            _loggerMock.Object);
    }

    #region ValidatePatches Tests

    [Fact]
    public void ValidatePatches_WithValidPatches_ReturnsNoErrors()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/name",
                Value = "Updated Name",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "Normalize capitalization"
            }
        };

        // Act
        var errors = _service.ValidatePatches(recipe, patches);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePatches_WithEmptyPath_ReturnsError()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "",
                Value = "value",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "test"
            }
        };

        // Act
        var errors = _service.ValidatePatches(recipe, patches);

        // Assert
        Assert.Single(errors);
        Assert.Contains("empty path", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePatches_WithPathNotStartingWithSlash_ReturnsError()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "name",
                Value = "value",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "test"
            }
        };

        // Act
        var errors = _service.ValidatePatches(recipe, patches);

        // Assert
        Assert.Single(errors);
        Assert.Contains("must start with '/'", errors[0]);
    }

    [Fact]
    public void ValidatePatches_WithMultipleInvalidPatches_ReturnsMultipleErrors()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "",
                Value = "value1",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "test1"
            },
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "invalid",
                Value = "value2",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "test2"
            }
        };

        // Act
        var errors = _service.ValidatePatches(recipe, patches);

        // Assert
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void ValidatePatches_WithEmptyPatchList_ReturnsNoErrors()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>();

        // Act
        var errors = _service.ValidatePatches(recipe, patches);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region ApplyPatchesAsync Tests

    [Fact]
    public async Task ApplyPatchesAsync_WithEmptyPatches_ReturnsSuccessWithOriginalRecipe()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>();

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("No patches to apply", result.Summary);
        Assert.Empty(result.AppliedPatches);
    }

    [Fact]
    public async Task ApplyPatchesAsync_WithValidReplacePatch_AppliesPatch()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/name",
                Value = "Updated Recipe Name",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "Normalize capitalization"
            }
        };

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.AppliedPatches);
        Assert.NotNull(result.NormalizedRecipe);
        Assert.Equal("Updated Recipe Name", result.NormalizedRecipe.Name);
    }

    [Fact]
    public async Task ApplyPatchesAsync_WithValidAddPatch_AddsValue()
    {
        // Arrange
        var recipe = new Recipe
        {
            Id = "test-id",
            Name = "Test Recipe"
        };
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Add,
                Path = "/description",
                Value = "A new description",
                RiskCategory = NormalizePatchRiskCategory.Medium,
                Reason = "Add missing description"
            }
        };

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.NormalizedRecipe);
        Assert.Equal("A new description", result.NormalizedRecipe.Description);
    }

    [Fact]
    public async Task ApplyPatchesAsync_WithInvalidPath_ReportsFailedPatch()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/nonexistent/deeply/nested/path",
                Value = "value",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "test"
            }
        };

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.FailedPatches);
        Assert.Contains("not found", result.FailedPatches[0].Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyPatchesAsync_WithMixedPatches_AppliesValidAndReportsInvalid()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/name",
                Value = "Valid Name Update",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "Valid patch"
            },
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/invalid/path",
                Value = "value",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "Invalid patch"
            }
        };

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.False(result.Success); // Partial success
        Assert.Single(result.AppliedPatches);
        Assert.Single(result.FailedPatches);
        Assert.NotNull(result.NormalizedRecipe);
        Assert.Equal("Valid Name Update", result.NormalizedRecipe.Name);
    }

    [Fact]
    public async Task ApplyPatchesAsync_StoresOriginalValue()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var originalName = recipe.Name;
        var patches = new List<NormalizePatchOperation>
        {
            new()
            {
                Op = JsonPatchOperationType.Replace,
                Path = "/name",
                Value = "New Name",
                RiskCategory = NormalizePatchRiskCategory.Low,
                Reason = "Update name"
            }
        };

        // Act
        var result = await _service.ApplyPatchesAsync(recipe, patches);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.AppliedPatches);
        Assert.NotNull(result.AppliedPatches[0].OriginalValue);
    }

    #endregion

    #region GeneratePatchesAsync Tests

    [Fact]
    public async Task GeneratePatchesAsync_CallsLlmWithRenderedPrompt()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var llmResponse = new LlmResponse
        {
            Content = JsonSerializer.Serialize(new NormalizePatchResponse
            {
                Patches = [],
                Summary = "No changes needed",
                HasHighRiskChanges = false
            })
        };
        
        _llmRouterMock
            .Setup(r => r.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.GeneratePatchesAsync(recipe);

        // Assert
        _llmRouterMock.Verify(r => r.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(result.Patches);
        Assert.Equal("No changes needed", result.Summary);
    }

    [Fact]
    public async Task GeneratePatchesAsync_WithFocusAreas_IncludesInPrompt()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var focusAreas = new List<string> { "capitalization", "units" };
        var llmResponse = new LlmResponse
        {
            Content = JsonSerializer.Serialize(new NormalizePatchResponse
            {
                Patches = [],
                Summary = "Focused on capitalization and units",
                HasHighRiskChanges = false
            })
        };
        
        _llmRouterMock
            .Setup(r => r.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.GeneratePatchesAsync(recipe, focusAreas);

        // Assert
        _promptRendererMock.Verify(
            r => r.Render(It.IsAny<string>(), It.Is<Dictionary<string, object?>>(d => d.ContainsKey("focus_areas"))),
            Times.Once);
    }

    [Fact]
    public async Task GeneratePatchesAsync_WithInvalidLlmResponse_ReturnsEmptyPatches()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var llmResponse = new LlmResponse
        {
            Content = "This is not valid JSON"
        };
        
        _llmRouterMock
            .Setup(r => r.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.GeneratePatchesAsync(recipe);

        // Assert
        Assert.Empty(result.Patches);
        Assert.Contains("Failed to parse", result.Summary);
    }

    [Fact]
    public async Task GeneratePatchesAsync_WithValidResponse_ParsesPatches()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var llmResponse = new LlmResponse
        {
            Content = JsonSerializer.Serialize(new NormalizePatchResponse
            {
                Patches = new List<NormalizePatchOperation>
                {
                    new()
                    {
                        Op = JsonPatchOperationType.Replace,
                        Path = "/name",
                        Value = "Improved Name",
                        RiskCategory = NormalizePatchRiskCategory.Low,
                        Reason = "Capitalize properly"
                    }
                },
                Summary = "1 low-risk change",
                HasHighRiskChanges = false
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };
        
        _llmRouterMock
            .Setup(r => r.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.GeneratePatchesAsync(recipe);

        // Assert
        Assert.Single(result.Patches);
        Assert.Equal("/name", result.Patches[0].Path);
        Assert.Equal(NormalizePatchRiskCategory.Low, result.Patches[0].RiskCategory);
    }

    #endregion

    #region NormalizePatchResponse Model Tests

    [Fact]
    public void NormalizePatchResponse_LowRiskCount_CalculatesCorrectly()
    {
        // Arrange
        var response = new NormalizePatchResponse
        {
            Patches = new List<NormalizePatchOperation>
            {
                CreatePatch(NormalizePatchRiskCategory.Low),
                CreatePatch(NormalizePatchRiskCategory.Low),
                CreatePatch(NormalizePatchRiskCategory.Medium),
                CreatePatch(NormalizePatchRiskCategory.High)
            }
        };

        // Assert
        Assert.Equal(2, response.LowRiskCount);
        Assert.Equal(1, response.MediumRiskCount);
        Assert.Equal(1, response.HighRiskCount);
    }

    [Fact]
    public void NormalizePatchResult_Succeeded_CreatesCorrectResult()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var patches = new List<NormalizePatchOperation>
        {
            CreatePatch(NormalizePatchRiskCategory.Low)
        };

        // Act
        var result = NormalizePatchResult.Succeeded(recipe, patches, "Success");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(recipe, result.NormalizedRecipe);
        Assert.Single(result.AppliedPatches);
        Assert.Empty(result.FailedPatches);
        Assert.Equal("Success", result.Summary);
    }

    [Fact]
    public void NormalizePatchResult_Partial_CreatesCorrectResult()
    {
        // Arrange
        var recipe = CreateTestRecipe();
        var applied = new List<NormalizePatchOperation> { CreatePatch(NormalizePatchRiskCategory.Low) };
        var failed = new List<NormalizePatchError>
        {
            new() { Patch = CreatePatch(NormalizePatchRiskCategory.Medium), Error = "Path not found" }
        };

        // Act
        var result = NormalizePatchResult.Partial(recipe, applied, failed, "Partial success");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(recipe, result.NormalizedRecipe);
        Assert.Single(result.AppliedPatches);
        Assert.Single(result.FailedPatches);
    }

    [Fact]
    public void NormalizePatchResult_Failed_CreatesCorrectResult()
    {
        // Act
        var result = NormalizePatchResult.Failed("Complete failure");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.NormalizedRecipe);
        Assert.Empty(result.AppliedPatches);
        Assert.Empty(result.FailedPatches);
        Assert.Equal("Complete failure", result.Summary);
    }

    #endregion

    #region Helper Methods

    private static Recipe CreateTestRecipe() => new()
    {
        Id = "test-recipe",
        Name = "Test Recipe",
        Description = "A test recipe description",
        Instructions = ["Step 1", "Step 2"],
        Ingredients =
        [
            new Ingredient { Name = "flour", Quantity = 2, Unit = "cups" }
        ]
    };

    private static NormalizePatchOperation CreatePatch(NormalizePatchRiskCategory riskCategory) => new()
    {
        Op = JsonPatchOperationType.Replace,
        Path = "/name",
        Value = "New Value",
        RiskCategory = riskCategory,
        Reason = "Test reason"
    };

    #endregion
}
