using Cookbook.Platform.Orchestrator.Services.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for SimilarityDetector service.
/// </summary>
public class SimilarityDetectorTests
{
    private readonly SimilarityDetector _detector;
    private readonly SimilarityOptions _options;

    public SimilarityDetectorTests()
    {
        _options = new SimilarityOptions
        {
            MaxContiguousOverlapThreshold = 50,
            MaxNgramSimilarityThreshold = 0.7,
            WarningOverlapThreshold = 25,
            WarningNgramThreshold = 0.5,
            NgramSize = 5,
            MinTokenLength = 2
        };

        _detector = new SimilarityDetector(
            NullLogger<SimilarityDetector>.Instance,
            Options.Create(_options));
    }

    #region M3-006: Tokenization Tests

    [Fact]
    public void Tokenize_SimpleText_ReturnsWords()
    {
        // Arrange
        var text = "Hello world this is a test";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Equal(["hello", "world", "this", "is", "test"], tokens);
    }

    [Fact]
    public void Tokenize_WithPunctuation_IgnoresPunctuation()
    {
        // Arrange
        var text = "Hello, world! This is a test.";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Contains("hello", tokens);
        Assert.Contains("world", tokens);
        Assert.Contains("test", tokens);
        Assert.DoesNotContain(",", tokens);
        Assert.DoesNotContain("!", tokens);
        Assert.DoesNotContain(".", tokens);
    }

    [Fact]
    public void Tokenize_MixedCase_ReturnsLowercase()
    {
        // Arrange
        var text = "Hello WORLD Test";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Equal(["hello", "world", "test"], tokens);
    }

    [Fact]
    public void Tokenize_WithNumbers_IncludesNumbers()
    {
        // Arrange
        var text = "Add 2 cups of flour and 350 grams of sugar";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Contains("cups", tokens);
        Assert.Contains("350", tokens);
        Assert.Contains("grams", tokens);
    }

    [Fact]
    public void Tokenize_ShortWords_FilteredByMinLength()
    {
        // Arrange
        var text = "I am a test of a short word filter";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        // "I", "a" should be filtered (length < 2)
        Assert.DoesNotContain("i", tokens);
        Assert.DoesNotContain("a", tokens);
        Assert.Contains("am", tokens);
        Assert.Contains("test", tokens);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var text = "";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_NullString_ReturnsEmptyArray()
    {
        // Act
        var tokens = _detector.Tokenize(null!);

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_WhitespaceOnly_ReturnsEmptyArray()
    {
        // Arrange
        var text = "   \t\n   ";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_RecipeInstructions_TokenizesCorrectly()
    {
        // Arrange
        var text = "Preheat oven to 350°F. Mix flour, sugar, and eggs in a large bowl.";

        // Act
        var tokens = _detector.Tokenize(text);

        // Assert
        Assert.Contains("preheat", tokens);
        Assert.Contains("oven", tokens);
        Assert.Contains("350", tokens);
        Assert.Contains("mix", tokens);
        Assert.Contains("flour", tokens);
        Assert.Contains("sugar", tokens);
        Assert.Contains("eggs", tokens);
    }

    #endregion

    #region M3-007: Token Overlap Tests

    [Fact]
    public void ComputeMaxContiguousOverlap_ExactMatch_ReturnsFullLength()
    {
        // Arrange
        var sourceTokens = new[] { "hello", "world", "test" };
        var extractedTokens = new[] { "hello", "world", "test" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(3, overlap);
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_PartialMatch_ReturnsMatchLength()
    {
        // Arrange
        var sourceTokens = new[] { "the", "quick", "brown", "fox", "jumps" };
        var extractedTokens = new[] { "brown", "fox" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(2, overlap);
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_NoMatch_ReturnsZero()
    {
        // Arrange
        var sourceTokens = new[] { "hello", "world" };
        var extractedTokens = new[] { "goodbye", "planet" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(0, overlap);
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_EmptySource_ReturnsZero()
    {
        // Arrange
        var sourceTokens = Array.Empty<string>();
        var extractedTokens = new[] { "hello", "world" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(0, overlap);
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_EmptyExtracted_ReturnsZero()
    {
        // Arrange
        var sourceTokens = new[] { "hello", "world" };
        var extractedTokens = Array.Empty<string>();

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(0, overlap);
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_MultipleMatches_ReturnsLongest()
    {
        // Arrange
        var sourceTokens = new[] { "mix", "flour", "sugar", "add", "eggs", "mix", "flour", "sugar", "butter" };
        var extractedTokens = new[] { "mix", "flour", "sugar", "butter" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(4, overlap); // "mix flour sugar butter" matches at the end
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_NonContiguousMatch_ReturnsContiguousPart()
    {
        // Arrange
        var sourceTokens = new[] { "preheat", "oven", "to", "350", "degrees" };
        var extractedTokens = new[] { "preheat", "oven", "bake", "at", "350" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(2, overlap); // "preheat oven" is the longest contiguous match
    }

    [Fact]
    public void ComputeMaxContiguousOverlap_SingleTokenMatch_ReturnsOne()
    {
        // Arrange
        var sourceTokens = new[] { "hello", "world" };
        var extractedTokens = new[] { "world" };

        // Act
        var overlap = _detector.ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);

        // Assert
        Assert.Equal(1, overlap);
    }

    #endregion

    #region M3-008: Jaccard Similarity Tests

    [Fact]
    public void ComputeNgramJaccardSimilarity_IdenticalText_ReturnsOne()
    {
        // Arrange
        var tokens = new[] { "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(tokens, tokens, 5);

        // Assert
        Assert.Equal(1.0, similarity, precision: 4);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_CompletelyDifferent_ReturnsZero()
    {
        // Arrange
        var source = new[] { "the", "quick", "brown", "fox", "jumps" };
        var extracted = new[] { "one", "two", "three", "four", "five" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_TooShortForNgrams_ReturnsZero()
    {
        // Arrange
        var source = new[] { "hello", "world" }; // Less than 5 tokens
        var extracted = new[] { "hello", "world" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_PartialOverlap_ReturnsFraction()
    {
        // Arrange
        var source = new[] { "mix", "flour", "sugar", "eggs", "butter", "vanilla", "salt" };
        var extracted = new[] { "mix", "flour", "sugar", "eggs", "butter", "baking", "powder" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        // Should be between 0 and 1
        Assert.True(similarity > 0.0);
        Assert.True(similarity < 1.0);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_EmptySource_ReturnsZero()
    {
        // Arrange
        var source = Array.Empty<string>();
        var extracted = new[] { "hello", "world", "test", "foo", "bar" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_EmptyExtracted_ReturnsZero()
    {
        // Arrange
        var source = new[] { "hello", "world", "test", "foo", "bar" };
        var extracted = Array.Empty<string>();

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_CustomNgramSize_Works()
    {
        // Arrange
        var source = new[] { "one", "two", "three" };
        var extracted = new[] { "one", "two", "three" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 3);

        // Assert
        Assert.Equal(1.0, similarity, precision: 4);
    }

    [Fact]
    public void ComputeNgramJaccardSimilarity_HighOverlap_ReturnsHighValue()
    {
        // Arrange - 90% similar tokens
        var source = new[] { "preheat", "oven", "to", "350", "degrees", "fahrenheit", "bake", "for", "30", "minutes" };
        var extracted = new[] { "preheat", "oven", "to", "350", "degrees", "fahrenheit", "bake", "for", "30", "mins" };

        // Act
        var similarity = _detector.ComputeNgramJaccardSimilarity(source, extracted, 5);

        // Assert
        Assert.True(similarity >= 0.5); // Should be reasonably high
    }

    #endregion

    #region AnalyzeAsync Tests

    [Fact]
    public async Task AnalyzeAsync_HighSimilarity_ViolatesPolicy()
    {
        // Arrange
        var source = "Preheat oven to 350 degrees. Mix flour sugar eggs and butter. Bake for 30 minutes until golden brown.";
        var extracted = "Preheat oven to 350 degrees. Mix flour sugar eggs and butter. Bake for 30 minutes until golden brown.";

        // Act
        var report = await _detector.AnalyzeAsync(source, extracted);

        // Assert
        Assert.True(report.ViolatesPolicy);
        Assert.True(report.MaxNgramSimilarity > 0.7);
    }

    [Fact]
    public async Task AnalyzeAsync_LowSimilarity_DoesNotViolate()
    {
        // Arrange
        var source = "The original recipe from a famous chef involves many complex techniques.";
        var extracted = "A simple approach using basic ingredients makes this dish easy to prepare.";

        // Act
        var report = await _detector.AnalyzeAsync(source, extracted);

        // Assert
        Assert.False(report.ViolatesPolicy);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyContent_ReturnsZeroSimilarity()
    {
        // Arrange
        var source = "";
        var extracted = "Some text";

        // Act
        var report = await _detector.AnalyzeAsync(source, extracted);

        // Assert
        Assert.Equal(0, report.MaxContiguousTokenOverlap);
        Assert.Equal(0.0, report.MaxNgramSimilarity);
        Assert.False(report.ViolatesPolicy);
    }

    [Fact]
    public async Task AnalyzeSectionsAsync_MultipleHighSimilaritySections_ReportsMaximum()
    {
        // Arrange
        var source = "Preheat oven to 350 degrees fahrenheit. Mix flour sugar eggs butter vanilla. Bake until golden brown and set.";
        var sections = new Dictionary<string, string>
        {
            ["Description"] = "A delicious cake recipe.",
            ["Instructions"] = "Preheat oven to 350 degrees fahrenheit. Mix flour sugar eggs butter vanilla. Bake until golden brown and set."
        };

        // Act
        var report = await _detector.AnalyzeSectionsAsync(source, sections);

        // Assert
        Assert.True(report.ViolatesPolicy);
        Assert.Contains("Instructions", report.Details ?? "");
    }

    #endregion
}
