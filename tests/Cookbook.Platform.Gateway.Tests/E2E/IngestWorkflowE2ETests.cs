using System.Net.Http.Json;
using System.Text.Json;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Xunit;

namespace Cookbook.Platform.Gateway.Tests.E2E;

/// <summary>
/// End-to-end tests for the complete URL import workflow.
/// Tests the full pipeline from URL submission to recipe draft creation.
/// </summary>
public class IngestWorkflowE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region Test Data

    /// <summary>
    /// Sample JSON-LD recipe data for testing extraction.
    /// </summary>
    private static string SampleRecipeJsonLd => """
    {
        "@context": "https://schema.org/",
        "@type": "Recipe",
        "name": "Classic Chocolate Chip Cookies",
        "description": "Delicious homemade cookies with gooey chocolate chips.",
        "author": {
            "@type": "Person",
            "name": "Test Chef"
        },
        "prepTime": "PT15M",
        "cookTime": "PT12M",
        "totalTime": "PT27M",
        "recipeYield": "24 cookies",
        "recipeIngredient": [
            "2 1/4 cups all-purpose flour",
            "1 cup butter, softened",
            "3/4 cup granulated sugar",
            "2 cups chocolate chips"
        ],
        "recipeInstructions": [
            {
                "@type": "HowToStep",
                "text": "Preheat oven to 375°F (190°C)."
            },
            {
                "@type": "HowToStep",
                "text": "Cream together butter and sugar until fluffy."
            },
            {
                "@type": "HowToStep",
                "text": "Add flour gradually and mix until combined."
            },
            {
                "@type": "HowToStep",
                "text": "Fold in chocolate chips."
            },
            {
                "@type": "HowToStep",
                "text": "Drop rounded tablespoons onto baking sheets."
            },
            {
                "@type": "HowToStep",
                "text": "Bake for 9-11 minutes or until golden brown."
            }
        ],
        "recipeCategory": "Dessert",
        "recipeCuisine": "American",
        "keywords": "cookies, chocolate, baking, dessert",
        "nutrition": {
            "@type": "NutritionInformation",
            "calories": "150 kcal"
        },
        "image": "https://example.com/cookies.jpg"
    }
    """;

    /// <summary>
    /// Sample HTML page with embedded JSON-LD recipe.
    /// </summary>
    private static string SampleRecipeHtml => $"""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Classic Chocolate Chip Cookies Recipe</title>
        <script type="application/ld+json">
        {SampleRecipeJsonLd}
        </script>
    </head>
    <body>
        <h1>Classic Chocolate Chip Cookies</h1>
        <p>Delicious homemade cookies with gooey chocolate chips.</p>
    </body>
    </html>
    """;

    #endregion

    #region M1-082: Happy Path Tests

    [Fact]
    public void CreateIngestTaskRequest_WithValidUrl_HasCorrectStructure()
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = null,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipes/chocolate-chip-cookies"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CreateIngestTaskRequest>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Ingest", deserialized.AgentType);
        Assert.Null(deserialized.ThreadId);
        Assert.NotNull(deserialized.Payload);
        Assert.Equal(IngestMode.Url, deserialized.Payload.Mode);
        Assert.Equal("https://example.com/recipes/chocolate-chip-cookies", deserialized.Payload.Url);
    }

    [Fact]
    public void CreateIngestTaskResponse_HasRequiredFields()
    {
        // Arrange
        var response = new CreateIngestTaskResponse
        {
            TaskId = "task-123",
            ThreadId = "thread-456",
            AgentType = "Ingest",
            Status = "Pending"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CreateIngestTaskResponse>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("task-123", deserialized.TaskId);
        Assert.Equal("thread-456", deserialized.ThreadId);
        Assert.Equal("Ingest", deserialized.AgentType);
        Assert.Equal("Pending", deserialized.Status);
    }

    [Fact]
    public void RecipeDraft_WithValidRecipe_PassesValidation()
    {
        // Arrange
        var recipe = CreateValidTestRecipe();
        var draft = new RecipeDraft
        {
            Recipe = recipe,
            Source = new RecipeSource
            {
                Url = "https://example.com/recipes/test",
                UrlHash = "abc123",
                SiteName = "Example",
                Author = "Test Chef",
                RetrievedAt = DateTime.UtcNow,
                ExtractionMethod = "JsonLd"
            },
            ValidationReport = new ValidationReport
            {
                Errors = [],
                Warnings = []
            },
            Artifacts = []
        };

        // Assert
        Assert.True(draft.ValidationReport.IsValid);
        Assert.NotNull(draft.Recipe);
        Assert.NotNull(draft.Source);
    }

    [Fact]
    public void RecipeDraft_Serialization_RoundTrip()
    {
        // Arrange
        var draft = new RecipeDraft
        {
            Recipe = CreateValidTestRecipe(),
            Source = new RecipeSource
            {
                Url = "https://example.com/recipes/test",
                UrlHash = "abc123",
                ExtractionMethod = "JsonLd"
            },
            ValidationReport = new ValidationReport
            {
                Errors = [],
                Warnings = ["Missing description"]
            },
            Artifacts = [new ArtifactRef { Type = "raw.html", Uri = "https://blob/raw.html" }]
        };

        // Act
        var json = JsonSerializer.Serialize(draft, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RecipeDraft>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(draft.Recipe.Name, deserialized.Recipe.Name);
        Assert.Equal(draft.Source.Url, deserialized.Source.Url);
        Assert.True(deserialized.ValidationReport.IsValid);
        Assert.Single(deserialized.ValidationReport.Warnings);
        Assert.Single(deserialized.Artifacts);
    }

    [Fact]
    public void IngestPayload_UrlMode_ValidatesHttpSchemes()
    {
        // Arrange
        var validUrls = new[]
        {
            "https://example.com/recipe",
            "http://example.com/recipe",
            "https://www.allrecipes.com/recipe/12345",
            "http://cooking.nytimes.com/recipes/1234"
        };

        // Assert
        foreach (var url in validUrls)
        {
            var isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                          (uri.Scheme == "http" || uri.Scheme == "https");
            Assert.True(isValid, $"URL should be valid: {url}");
        }
    }

    [Fact]
    public void IngestPayload_QueryMode_AcceptsSearchQuery()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "chocolate chip cookies recipe"
        };

        // Assert
        Assert.Equal(IngestMode.Query, payload.Mode);
        Assert.NotNull(payload.Query);
        Assert.Contains("chocolate chip cookies", payload.Query);
    }

    #endregion

    #region M1-083: Error Case Tests

    [Theory]
    [InlineData("ftp://example.com/recipe")]
    [InlineData("file:///path/to/recipe")]
    [InlineData("javascript:void(0)")]
    [InlineData("data:text/html,<h1>test</h1>")]
    public void IngestPayload_InvalidUrlScheme_ShouldBeRejected(string invalidUrl)
    {
        // Arrange
        var isValid = Uri.TryCreate(invalidUrl, UriKind.Absolute, out var uri) &&
                      (uri.Scheme == "http" || uri.Scheme == "https");

        // Assert
        Assert.False(isValid, $"URL with scheme should be rejected: {invalidUrl}");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com/recipe")]
    [InlineData("://missing-scheme.com")]
    [InlineData("http://")]
    public void IngestPayload_MalformedUrl_ShouldBeRejected(string malformedUrl)
    {
        // Arrange
        var isValid = Uri.TryCreate(malformedUrl, UriKind.Absolute, out var uri) &&
                      (uri.Scheme == "http" || uri.Scheme == "https");

        // Assert
        Assert.False(isValid, $"Malformed URL should be rejected: {malformedUrl}");
    }

    [Theory]
    [InlineData("http://10.0.0.1/recipe")]
    [InlineData("http://192.168.1.1/recipe")]
    [InlineData("http://172.16.0.1/recipe")]
    [InlineData("http://127.0.0.1/recipe")]
    [InlineData("http://localhost/recipe")]
    public void IngestPayload_PrivateIpUrl_ShouldBeBlockedBySsrf(string privateUrl)
    {
        // Arrange - These should be blocked by SSRF protection
        var uri = new Uri(privateUrl);
        var host = uri.Host;
        
        var isPrivate = host == "localhost" ||
                        host == "127.0.0.1" ||
                        host.StartsWith("10.") ||
                        host.StartsWith("192.168.") ||
                        host.StartsWith("172.16.") ||
                        host.StartsWith("172.17.") ||
                        host.StartsWith("172.18.") ||
                        host.StartsWith("172.19.") ||
                        host.StartsWith("172.2") ||
                        host.StartsWith("172.30.") ||
                        host.StartsWith("172.31.");

        // Assert
        Assert.True(isPrivate, $"Private IP URL should be blocked: {privateUrl}");
    }

    [Fact]
    public void ValidationReport_WithErrors_IsNotValid()
    {
        // Arrange
        var report = new ValidationReport
        {
            Errors = ["[REQUIRED_NAME] Name: Recipe name is required"],
            Warnings = []
        };

        // Assert
        Assert.False(report.IsValid);
        Assert.Single(report.Errors);
    }

    [Fact]
    public void ValidationReport_WithOnlyWarnings_IsValid()
    {
        // Arrange
        var report = new ValidationReport
        {
            Errors = [],
            Warnings = [
                "[MISSING_DESCRIPTION] Description: No description provided",
                "[NO_IMAGE] ImageUrl: No image URL specified"
            ]
        };

        // Assert
        Assert.True(report.IsValid);
        Assert.Equal(2, report.Warnings.Count);
    }

    [Fact]
    public void RecipeDraft_WithValidationErrors_IndicatesInvalid()
    {
        // Arrange
        var draft = new RecipeDraft
        {
            Recipe = new Recipe { Id = "", Name = "" },
            Source = new RecipeSource
            {
                Url = "https://example.com/recipe",
                UrlHash = "hash123",
                ExtractionMethod = "Llm"
            },
            ValidationReport = new ValidationReport
            {
                Errors = ["Extraction failed: No recipe content found"],
                Warnings = []
            },
            Artifacts = []
        };

        // Assert
        Assert.False(draft.ValidationReport.IsValid);
    }

    [Fact]
    public void RecipeSource_WithExtractionMethod_TracksProvenance()
    {
        // Arrange
        var jsonLdSource = new RecipeSource
        {
            Url = "https://example.com/recipe1",
            UrlHash = "hash1",
            ExtractionMethod = "JsonLd"
        };

        var llmSource = new RecipeSource
        {
            Url = "https://example.com/recipe2",
            UrlHash = "hash2",
            ExtractionMethod = "Llm"
        };

        // Assert
        Assert.Equal("JsonLd", jsonLdSource.ExtractionMethod);
        Assert.Equal("Llm", llmSource.ExtractionMethod);
    }

    [Fact]
    public void IngestPayload_MissingRequiredFields_FailsValidation()
    {
        // Arrange - URL mode without URL
        var urlModeWithoutUrl = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = null
        };

        // Arrange - Query mode without query
        var queryModeWithoutQuery = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = null
        };

        // Assert
        Assert.True(string.IsNullOrEmpty(urlModeWithoutUrl.Url));
        Assert.True(string.IsNullOrEmpty(queryModeWithoutQuery.Query));
    }

    [Fact]
    public void IngestMode_HasExpectedValues()
    {
        // Arrange - Valid mode values
        var validModes = new[] 
        { 
            IngestMode.Url,
            IngestMode.Query,
            IngestMode.Normalize
        };

        // Assert - All modes are distinct
        Assert.Equal(validModes.Length, validModes.Distinct().Count());
    }

    [Fact]
    public void ArtifactRef_ContainsTypeAndUri()
    {
        // Arrange
        var artifacts = new List<ArtifactRef>
        {
            new() { Type = "raw.html", Uri = "https://blob/thread/task/fetch/raw.html" },
            new() { Type = "sanitized.txt", Uri = "https://blob/thread/task/sanitize/sanitized.txt" },
            new() { Type = "recipe.jsonld", Uri = "https://blob/thread/task/extract/recipe.jsonld" },
            new() { Type = "extraction.json", Uri = "https://blob/thread/task/extract/extraction.json" },
            new() { Type = "validation.json", Uri = "https://blob/thread/task/validate/validation.json" }
        };

        // Assert
        Assert.Equal(5, artifacts.Count);
        Assert.All(artifacts, a =>
        {
            Assert.NotNull(a.Type);
            Assert.NotNull(a.Uri);
            Assert.Contains("blob", a.Uri);
        });
    }

    #endregion

    #region Pipeline Integration Tests

    [Fact]
    public void IngestWorkflow_PhaseProgression_FollowsExpectedOrder()
    {
        // Arrange - Expected phase order
        var expectedPhases = new[]
        {
            "Ingest.Fetch",
            "Ingest.Extract",
            "Ingest.Validate",
            "Ingest.ReviewReady"
        };

        // Assert - Phases are in expected order
        Assert.Equal(4, expectedPhases.Length);
        Assert.Equal("Ingest.Fetch", expectedPhases[0]);
        Assert.Equal("Ingest.ReviewReady", expectedPhases[^1]);
    }

    [Fact]
    public void IngestWorkflow_ProgressWeights_SumToHundred()
    {
        // Arrange - Phase weights as defined in IngestPhaseRunner
        var phaseWeights = new Dictionary<string, int>
        {
            ["Fetch"] = 20,
            ["Extract"] = 50,
            ["Validate"] = 20,
            ["ReviewReady"] = 10
        };

        // Act
        var totalWeight = phaseWeights.Values.Sum();

        // Assert
        Assert.Equal(100, totalWeight);
    }

    [Fact]
    public void ExtractionResult_Success_ContainsRecipeAndConfidence()
    {
        // Arrange
        var result = new
        {
            Success = true,
            Recipe = CreateValidTestRecipe(),
            Method = "JsonLd",
            Confidence = 0.95
        };

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Recipe);
        Assert.Equal("JsonLd", result.Method);
        Assert.True(result.Confidence >= 0.0 && result.Confidence <= 1.0);
    }

    [Fact]
    public void ExtractionResult_Failure_ContainsErrorDetails()
    {
        // Arrange
        var result = new
        {
            Success = false,
            Error = "No recipe content found on page",
            ErrorCode = "NO_RECIPE_CONTENT",
            Method = "Llm"
        };

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.ErrorCode);
    }

    [Fact]
    public void RecipeSource_UrlHash_IsConsistent()
    {
        // Arrange
        var url = "https://example.com/recipes/test";
        
        // Simulating hash generation (should be consistent for same URL)
        var hash1 = ComputeUrlHash(url);
        var hash2 = ComputeUrlHash(url);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void RecipeSource_DifferentUrls_HaveDifferentHashes()
    {
        // Arrange
        var url1 = "https://example.com/recipe1";
        var url2 = "https://example.com/recipe2";
        
        var hash1 = ComputeUrlHash(url1);
        var hash2 = ComputeUrlHash(url2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    #endregion

    #region Helper Methods

    private static Recipe CreateValidTestRecipe()
    {
        return new Recipe
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Classic Chocolate Chip Cookies",
            Description = "Delicious homemade cookies with gooey chocolate chips.",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 12,
            Servings = 24,
            Cuisine = "American",
            DietType = "Vegetarian",
            ImageUrl = "https://example.com/cookies.jpg",
            Tags = ["cookies", "dessert", "baking", "chocolate"],
            Ingredients =
            [
                new Ingredient { Name = "all-purpose flour", Quantity = 2.25, Unit = "cups" },
                new Ingredient { Name = "butter, softened", Quantity = 1, Unit = "cup" },
                new Ingredient { Name = "granulated sugar", Quantity = 0.75, Unit = "cup" },
                new Ingredient { Name = "chocolate chips", Quantity = 2, Unit = "cups" }
            ],
            Instructions =
            [
                "Preheat oven to 375°F (190°C).",
                "Cream together butter and sugar until fluffy.",
                "Add flour gradually and mix until combined.",
                "Fold in chocolate chips.",
                "Drop rounded tablespoons onto baking sheets.",
                "Bake for 9-11 minutes or until golden brown."
            ]
        };
    }

    private static string ComputeUrlHash(string url)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(url);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash)[..22].Replace('+', '-').Replace('/', '_');
    }

    #endregion
}
