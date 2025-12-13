using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for LlmRecipeExtractor.
/// </summary>
public class LlmRecipeExtractorTests
{
    private readonly Mock<ILlmRouter> _llmRouterMock;
    private readonly Mock<ILogger<LlmRecipeExtractor>> _loggerMock;
    private readonly IngestOptions _options;
    private readonly LlmRecipeExtractor _extractor;

    public LlmRecipeExtractorTests()
    {
        _llmRouterMock = new Mock<ILlmRouter>();
        _loggerMock = new Mock<ILogger<LlmRecipeExtractor>>();
        _options = new IngestOptions();
        
        _extractor = new LlmRecipeExtractor(
            _llmRouterMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    #region CanExtract Tests

    [Theory]
    [InlineData("Some recipe content", true)]
    [InlineData("   text   ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void CanExtract_ReturnsCorrectValue(string? content, bool expected)
    {
        var result = _extractor.CanExtract(content!);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Basic Extraction Tests

    [Fact]
    public async Task ExtractAsync_EmptyContent_ReturnsFailure()
    {
        var result = await _extractor.ExtractAsync("", new ExtractionContext());

        Assert.False(result.Success);
        Assert.Equal("EMPTY_CONTENT", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_ValidLlmResponse_ReturnsSuccess()
    {
        var llmResponse = """
            {
                "name": "LLM Extracted Recipe",
                "description": "A recipe extracted by LLM",
                "prepTimeMinutes": 15,
                "cookTimeMinutes": 30,
                "servings": 4,
                "ingredients": [
                    {"name": "flour", "quantity": 2, "unit": "cups"},
                    {"name": "sugar", "quantity": 1, "unit": "cup"}
                ],
                "instructions": [
                    "Mix ingredients",
                    "Bake at 350F"
                ]
            }
            """;

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _extractor.ExtractAsync("Some recipe text content", new ExtractionContext());

        Assert.True(result.Success);
        Assert.NotNull(result.Recipe);
        Assert.Equal("LLM Extracted Recipe", result.Recipe.Name);
        Assert.Equal(15, result.Recipe.PrepTimeMinutes);
        Assert.Equal(30, result.Recipe.CookTimeMinutes);
        Assert.Equal(4, result.Recipe.Servings);
        Assert.Equal(2, result.Recipe.Ingredients.Count);
        Assert.Equal(2, result.Recipe.Instructions.Count);
        Assert.Equal(ExtractionMethod.Llm, result.Method);
    }

    [Fact]
    public async Task ExtractAsync_SetsConfidenceBasedOnRepairs()
    {
        var llmResponse = """{"name": "Test Recipe", "servings": 4}""";

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _extractor.ExtractAsync("content", new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(0.85, result.Confidence); // No repairs = higher confidence
        Assert.Equal(0, result.RepairAttempts);
    }

    #endregion

    #region Content Truncation Tests

    [Fact]
    public async Task ExtractAsync_TruncatesLongContent()
    {
        var longContent = new string('x', 100_000);
        var llmResponse = """{"name": "Test Recipe"}""";

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var context = new ExtractionContext { ContentBudget = 1000 };
        var result = await _extractor.ExtractAsync(longContent, context);

        Assert.True(result.Success);
        
        // Verify the LLM was called with truncated content
        _llmRouterMock.Verify(x => x.ChatAsync(
            It.Is<LlmRequest>(r => r.Messages[0].Content.Length < longContent.Length),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region Markdown Code Block Handling Tests

    [Fact]
    public async Task ExtractAsync_HandlesMarkdownCodeBlocks()
    {
        var llmResponse = """
            ```json
            {"name": "Recipe in Code Block", "servings": 6}
            ```
            """;

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _extractor.ExtractAsync("content", new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal("Recipe in Code Block", result.Recipe?.Name);
    }

    #endregion

    #region LLM Error Handling Tests

    [Fact]
    public async Task ExtractAsync_LlmReturnsNoRecipeName_ReturnsFailure()
    {
        var llmResponse = """{"description": "No name field", "servings": 4}""";

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _extractor.ExtractAsync("content", new ExtractionContext());

        Assert.False(result.Success);
        Assert.Equal("LLM_EXTRACTION_FAILED", result.ErrorCode);
    }

    #endregion

    #region Request Metadata Tests

    [Fact]
    public async Task ExtractAsync_SetsLowTemperature()
    {
        var llmResponse = """{"name": "Test"}""";

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        await _extractor.ExtractAsync("content", new ExtractionContext());

        _llmRouterMock.Verify(x => x.ChatAsync(
            It.Is<LlmRequest>(r => r.Temperature == 0.3),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExtractAsync_SetsMetadata()
    {
        var llmResponse = """{"name": "Test"}""";

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        await _extractor.ExtractAsync("content", new ExtractionContext());

        _llmRouterMock.Verify(x => x.ChatAsync(
            It.Is<LlmRequest>(r => 
                r.Metadata.ContainsKey("purpose") && 
                r.Metadata["purpose"].ToString() == "recipe_extraction"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion
}

/// <summary>
/// Unit tests for RecipeExtractionOrchestrator.
/// </summary>
public class RecipeExtractionOrchestratorTests
{
    private readonly Mock<ILogger<JsonLdRecipeExtractor>> _jsonLdLoggerMock;
    private readonly Mock<ILlmRouter> _llmRouterMock;
    private readonly Mock<ILogger<LlmRecipeExtractor>> _llmLoggerMock;
    private readonly Mock<ILogger<RecipeExtractionOrchestrator>> _orchestratorLoggerMock;
    private readonly RecipeExtractionOrchestrator _orchestrator;

    public RecipeExtractionOrchestratorTests()
    {
        _jsonLdLoggerMock = new Mock<ILogger<JsonLdRecipeExtractor>>();
        _llmRouterMock = new Mock<ILlmRouter>();
        _llmLoggerMock = new Mock<ILogger<LlmRecipeExtractor>>();
        _orchestratorLoggerMock = new Mock<ILogger<RecipeExtractionOrchestrator>>();

        var jsonLdExtractor = new JsonLdRecipeExtractor(_jsonLdLoggerMock.Object);
        var llmExtractor = new LlmRecipeExtractor(
            _llmRouterMock.Object,
            Options.Create(new IngestOptions()),
            _llmLoggerMock.Object);

        _orchestrator = new RecipeExtractionOrchestrator(
            jsonLdExtractor,
            llmExtractor,
            _orchestratorLoggerMock.Object);
    }

    #region JSON-LD Priority Tests

    [Fact]
    public async Task ExtractAsync_WithValidJsonLd_UsesJsonLdMethod()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe text content",
            RecipeJsonLd = """{"@type": "Recipe", "name": "JSON-LD Recipe"}"""
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Equal("JSON-LD Recipe", result.Recipe?.Name);
        Assert.Equal(ExtractionMethod.JsonLd, result.Method);
        
        // LLM should not be called
        _llmRouterMock.Verify(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidJsonLd_FallsBackToLlm()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe text content",
            RecipeJsonLd = """{"@type": "Recipe"}""" // Missing name
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var llmResponse = """{"name": "LLM Fallback Recipe", "servings": 4}""";
        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Equal("LLM Fallback Recipe", result.Recipe?.Name);
        Assert.Equal(ExtractionMethod.Llm, result.Method);
    }

    [Fact]
    public async Task ExtractAsync_WithNoJsonLd_UsesLlm()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe text content",
            RecipeJsonLd = null
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var llmResponse = """{"name": "LLM Only Recipe", "servings": 4}""";
        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Equal("LLM Only Recipe", result.Recipe?.Name);
        Assert.Equal(ExtractionMethod.Llm, result.Method);
    }

    #endregion

    #region Source Population Tests

    [Fact]
    public async Task ExtractAsync_PopulatesSource()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe content",
            RecipeJsonLd = """{"@type": "Recipe", "name": "Test Recipe"}""",
            Metadata = new PageMetadata
            {
                SiteName = "Cooking Blog",
                Author = "Chef John"
            }
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.NotNull(result.Source);
        Assert.Equal("https://example.com/recipe", result.Source.Url);
        Assert.Equal("Cooking Blog", result.Source.SiteName);
        Assert.Equal("Chef John", result.Source.Author);
        Assert.Equal("JsonLd", result.Source.ExtractionMethod);
        Assert.NotEmpty(result.Source.UrlHash);
    }

    [Fact]
    public async Task ExtractAsync_SetsExtractionMethod_InSource()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe content",
            RecipeJsonLd = null
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var llmResponse = """{"name": "LLM Recipe"}""";
        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = llmResponse });

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Equal("Llm", result.Source?.ExtractionMethod);
    }

    #endregion

    #region Failure Handling Tests

    [Fact]
    public async Task ExtractAsync_BothMethodsFail_ReturnsFailure()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Some content",
            RecipeJsonLd = """{"invalid": "json-ld"}""" // Invalid (no name)
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        // LLM also returns invalid response
        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = """{"no_name": true}""" });

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    #endregion

    #region RawJsonLd Tests

    [Fact]
    public async Task ExtractAsync_WithJsonLd_IncludesRawJsonLd()
    {
        var jsonLd = """{"@type": "Recipe", "name": "Test Recipe"}""";
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe content",
            RecipeJsonLd = jsonLd
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Equal(jsonLd, result.RawJsonLd);
    }

    [Fact]
    public async Task ExtractAsync_WithLlm_NoRawJsonLd()
    {
        var sanitizedContent = new SanitizedContent
        {
            TextContent = "Recipe content",
            RecipeJsonLd = null
        };
        var context = new ExtractionContext { SourceUrl = "https://example.com/recipe" };

        _llmRouterMock.Setup(x => x.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = """{"name": "LLM Recipe"}""" });

        var result = await _orchestrator.ExtractAsync(sanitizedContent, context);

        Assert.True(result.Success);
        Assert.Null(result.RawJsonLd);
    }

    #endregion
}

/// <summary>
/// Tests for RecipeExtractionResult record.
/// </summary>
public class RecipeExtractionResultTests
{
    [Fact]
    public void RecipeExtractionResult_SuccessHasRecipeAndSource()
    {
        var result = new RecipeExtractionResult
        {
            Success = true,
            Recipe = new Recipe { Id = "1", Name = "Test" },
            Source = new RecipeSource { Url = "https://example.com", UrlHash = "abc123" },
            Method = ExtractionMethod.JsonLd
        };

        Assert.True(result.Success);
        Assert.NotNull(result.Recipe);
        Assert.NotNull(result.Source);
    }

    [Fact]
    public void RecipeExtractionResult_FailureHasError()
    {
        var result = new RecipeExtractionResult
        {
            Success = false,
            Error = "Extraction failed",
            ErrorCode = "FAILED"
        };

        Assert.False(result.Success);
        Assert.Null(result.Recipe);
        Assert.Equal("Extraction failed", result.Error);
    }
}
