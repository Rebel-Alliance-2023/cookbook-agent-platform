using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for RepairParaphraseService.
/// </summary>
public class RepairParaphraseServiceTests
{
    private readonly Mock<ILlmRouter> _llmRouterMock;
    private readonly Mock<IPromptRepository> _promptRepositoryMock;
    private readonly Mock<ISimilarityDetector> _similarityDetectorMock;
    private readonly IngestGuardrailOptions _guardrailOptions;
    private readonly RepairParaphraseService _service;

    public RepairParaphraseServiceTests()
    {
        _llmRouterMock = new Mock<ILlmRouter>();
        _promptRepositoryMock = new Mock<IPromptRepository>();
        _similarityDetectorMock = new Mock<ISimilarityDetector>();
        _guardrailOptions = new IngestGuardrailOptions
        {
            TokenOverlapWarningThreshold = 40,
            TokenOverlapErrorThreshold = 80,
            NgramSimilarityWarningThreshold = 0.20,
            NgramSimilarityErrorThreshold = 0.35,
            AutoRepairOnError = true
        };

        _service = new RepairParaphraseService(
            _llmRouterMock.Object,
            _promptRepositoryMock.Object,
            _similarityDetectorMock.Object,
            Options.Create(_guardrailOptions),
            NullLogger<RepairParaphraseService>.Instance);
    }

    private RecipeDraft CreateTestDraft(string description = "Test description", List<string>? instructions = null)
    {
        return new RecipeDraft
        {
            Recipe = new Recipe
            {
                Id = "test-recipe-id",
                Name = "Test Recipe",
                Description = description,
                Instructions = instructions ?? ["Step 1: Mix ingredients", "Step 2: Bake for 30 minutes"]
            },
            Source = new RecipeSource
            {
                Url = "https://example.com/recipe",
                UrlHash = "testhash123",
                RetrievedAt = DateTime.UtcNow,
                ExtractionMethod = "Test"
            },
            ValidationReport = new ValidationReport()
        };
    }

    private SimilarityReport CreateViolatingReport()
    {
        return new SimilarityReport
        {
            MaxContiguousTokenOverlap = 100,
            MaxNgramSimilarity = 0.85,
            ViolatesPolicy = true,
            Details = "High similarity detected"
        };
    }

    private SimilarityReport CreateNonViolatingReport()
    {
        return new SimilarityReport
        {
            MaxContiguousTokenOverlap = 20,
            MaxNgramSimilarity = 0.15,
            ViolatesPolicy = false,
            Details = "Low similarity"
        };
    }

    #region M3-021: Test: high similarity triggers warning

    [Fact]
    public async Task RepairAsync_HighSimilarity_AttemptsRepair()
    {
        // Arrange
        var draft = CreateTestDraft("This is a high similarity description copied verbatim");
        var sourceText = "This is a high similarity description copied verbatim from the source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["test", "tokens"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.25);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Description"", ""rephrased_text"": ""A fresh new description""}]}"
            });

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNonViolatingReport());

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        _llmRouterMock.Verify(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepairAsync_NoSectionsToRepair_ReturnsSuccessWithoutLlmCall()
    {
        // Arrange
        var draft = CreateTestDraft(description: ""); // Empty description
        draft = draft with { Recipe = draft.Recipe with { Instructions = [] } }; // No instructions
        var sourceText = "Some source text";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns([]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(0);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.0);

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("No sections required repair", result.Details ?? "");
        _llmRouterMock.Verify(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepairAsync_ViolatingReport_IdentifiesSectionsForRepair()
    {
        // Arrange
        var draft = CreateTestDraft("High similarity description that needs repair");
        var sourceText = "High similarity description that needs repair from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["high", "similarity", "description", "needs", "repair"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50); // Above warning threshold
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30); // Above warning threshold

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Description"", ""rephrased_text"": ""A rephrased description""}]}"
            });

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNonViolatingReport());

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert - LLM was called because sections needed repair
        _llmRouterMock.Verify(l => l.ChatAsync(
            It.Is<LlmRequest>(r => r.Messages.Any(m => m.Content.Contains("Description"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region M3-022: Test: AutoRepair reduces similarity

    [Fact]
    public async Task RepairAsync_SuccessfulRepair_ReducesSimilarity()
    {
        // Arrange
        var draft = CreateTestDraft("Original high similarity content");
        var sourceText = "Original high similarity content from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["original", "high", "similarity", "content"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Description"", ""rephrased_text"": ""Completely rewritten content with different words""}]}"
            });

        var reducedSimilarityReport = new SimilarityReport
        {
            MaxContiguousTokenOverlap = 10,
            MaxNgramSimilarity = 0.10,
            ViolatesPolicy = false
        };

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reducedSimilarityReport);

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.StillViolatesPolicy);
        Assert.NotNull(result.NewSimilarityReport);
        Assert.Equal(0.10, result.NewSimilarityReport!.MaxNgramSimilarity);
        Assert.Equal(10, result.NewSimilarityReport.MaxContiguousTokenOverlap);
    }

    [Fact]
    public async Task RepairAsync_UpdatesDraftWithRephrasedContent()
    {
        // Arrange
        var draft = CreateTestDraft("Original description");
        var sourceText = "Original description from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["original", "description"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Description"", ""rephrased_text"": ""Brand new description""}]}"
            });

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNonViolatingReport());

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.NotNull(result.RepairedDraft);
        Assert.Equal("Brand new description", result.RepairedDraft!.Recipe.Description);
    }

    [Fact]
    public async Task RepairAsync_RepairStillViolates_ReportsStillViolatesPolicy()
    {
        // Arrange
        var draft = CreateTestDraft("Very problematic content");
        var sourceText = "Very problematic content from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["very", "problematic", "content"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Description"", ""rephrased_text"": ""Still similar content""}]}"
            });

        // Still violating after repair
        var stillViolatingReport = new SimilarityReport
        {
            MaxContiguousTokenOverlap = 60,
            MaxNgramSimilarity = 0.75,
            ViolatesPolicy = true
        };

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stillViolatingReport);

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.StillViolatesPolicy);
    }

    [Fact]
    public async Task RepairAsync_LlmFails_ReturnsError()
    {
        // Arrange
        var draft = CreateTestDraft("Content to repair");
        var sourceText = "Content to repair from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["content", "repair"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmResponse?)null);

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("LLM did not return", result.Error ?? "");
    }

    [Fact]
    public async Task RepairAsync_InstructionsRepair_SplitsIntoSteps()
    {
        // Arrange
        var draft = CreateTestDraft(description: "", instructions: ["Original step copied verbatim"]);
        var sourceText = "Original step copied verbatim from source";
        var violatingReport = CreateViolatingReport();

        _similarityDetectorMock
            .Setup(s => s.Tokenize(It.IsAny<string>()))
            .Returns(["original", "step", "copied"]);
        _similarityDetectorMock
            .Setup(s => s.ComputeMaxContiguousOverlap(It.IsAny<string[]>(), It.IsAny<string[]>()))
            .Returns(50);
        _similarityDetectorMock
            .Setup(s => s.ComputeNgramJaccardSimilarity(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<int>()))
            .Returns(0.30);

        _llmRouterMock
            .Setup(l => l.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""sections"": [{""name"": ""Instructions"", ""rephrased_text"": ""1. First step rewritten.\n2. Second step added.""}]}"
            });

        _similarityDetectorMock
            .Setup(s => s.AnalyzeSectionsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNonViolatingReport());

        // Act
        var result = await _service.RepairAsync(draft, sourceText, violatingReport);

        // Assert
        Assert.NotNull(result.RepairedDraft);
        Assert.True(result.RepairedDraft!.Recipe.Instructions.Count >= 1);
    }

    #endregion
}
