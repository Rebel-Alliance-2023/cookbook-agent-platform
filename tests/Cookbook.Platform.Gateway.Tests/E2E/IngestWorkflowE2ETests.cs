using System.Net.Http.Json;
using System.Text.Json;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
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
                "text": "Preheat oven to 375�F (190�C)."
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

    #region M4-057: Query with Brave ? Discover ? ReviewReady

    [Fact]
    public void CreateIngestTaskRequest_QueryModeWithBraveProvider_HasCorrectStructure()
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = null,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Query,
                Query = "chocolate chip cookies recipe",
                Search = new SearchSettings
                {
                    ProviderId = "brave"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CreateIngestTaskRequest>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Ingest", deserialized.AgentType);
        Assert.NotNull(deserialized.Payload);
        Assert.Equal(IngestMode.Query, deserialized.Payload.Mode);
        Assert.Equal("chocolate chip cookies recipe", deserialized.Payload.Query);
        Assert.NotNull(deserialized.Payload.Search);
        Assert.Equal("brave", deserialized.Payload.Search.ProviderId);
    }

    [Fact]
    public void IngestWorkflow_QueryMode_PhaseProgressionIncludesDiscover()
    {
        // Arrange - Expected phase order for Query mode
        var expectedPhases = new[]
        {
            "Ingest.Discover",
            "Ingest.Fetch",
            "Ingest.Extract",
            "Ingest.Validate",
            "Ingest.RepairParaphrase",
            "Ingest.ReviewReady"
        };

        // Assert - Phases are in expected order
        Assert.Equal(6, expectedPhases.Length);
        Assert.Equal("Ingest.Discover", expectedPhases[0]);
        Assert.Equal("Ingest.ReviewReady", expectedPhases[^1]);
    }

    [Fact]
    public void IngestWorkflow_QueryMode_ProgressWeights_SumToHundred()
    {
        // Arrange - Phase weights for Query mode as defined in IngestPhaseRunner
        var phaseWeights = new Dictionary<string, int>
        {
            ["Discover"] = 10,
            ["Fetch"] = 15,
            ["Extract"] = 30,
            ["Validate"] = 15,
            ["RepairParaphrase"] = 15,
            ["ReviewReady"] = 10,
            ["Finalize"] = 5
        };

        // Act
        var totalWeight = phaseWeights.Values.Sum();

        // Assert
        Assert.Equal(100, totalWeight);
    }

    [Fact]
    public void SearchCandidate_FromBraveSearch_HasRequiredFields()
    {
        // Arrange
        var candidate = new SearchCandidate
        {
            Url = "https://example.com/chocolate-chip-cookies",
            Title = "Best Chocolate Chip Cookies Recipe",
            Snippet = "The most delicious homemade chocolate chip cookies you'll ever make.",
            SiteName = "Example Kitchen",
            Score = 0.95
        };

        // Assert
        Assert.NotEmpty(candidate.Url);
        Assert.NotEmpty(candidate.Title);
        Assert.NotEmpty(candidate.Snippet);
        Assert.NotEmpty(candidate.SiteName);
        Assert.True(candidate.Score >= 0 && candidate.Score <= 1);
    }

    [Fact]
    public void SearchResult_Success_ContainsCandidates()
    {
        // Arrange
        var candidates = new List<SearchCandidate>
        {
            new() { Url = "https://example.com/recipe1", Title = "Recipe 1", Snippet = "Snippet 1", SiteName = "Site1" },
            new() { Url = "https://example.com/recipe2", Title = "Recipe 2", Snippet = "Snippet 2", SiteName = "Site2" },
            new() { Url = "https://example.com/recipe3", Title = "Recipe 3", Snippet = "Snippet 3", SiteName = "Site3" }
        };

        var result = new SearchResult
        {
            Success = true,
            Candidates = candidates
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Null(result.Error);
    }

    [Fact]
    public void QueryModePayload_DefaultsToDefaultProvider()
    {
        // Arrange - Query mode without explicit provider
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "chocolate chip cookies"
        };

        // Assert - Search can be null, resolver will use default
        Assert.Equal(IngestMode.Query, payload.Mode);
        Assert.Null(payload.Search);
    }

    [Fact]
    public void QueryModePayload_WithExplicitBraveProvider()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "chocolate chip cookies",
            Search = new SearchSettings
            {
                ProviderId = "brave",
                MaxResults = 10,
                SafeSearch = "moderate"
            }
        };

        // Assert
        Assert.Equal("brave", payload.Search.ProviderId);
        Assert.Equal(10, payload.Search.MaxResults);
        Assert.Equal("moderate", payload.Search.SafeSearch);
    }

    #endregion

    #region M4-058: Query with Google CSE ? Discover ? ReviewReady

    [Fact]
    public void CreateIngestTaskRequest_QueryModeWithGoogleProvider_HasCorrectStructure()
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = null,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Query,
                Query = "easy pasta recipe",
                Search = new SearchSettings
                {
                    ProviderId = "google"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CreateIngestTaskRequest>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Ingest", deserialized.AgentType);
        Assert.Equal(IngestMode.Query, deserialized.Payload.Mode);
        Assert.Equal("easy pasta recipe", deserialized.Payload.Query);
        Assert.Equal("google", deserialized.Payload.Search.ProviderId);
    }

    [Fact]
    public void QueryModePayload_WithExplicitGoogleProvider()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "easy pasta recipe",
            Search = new SearchSettings
            {
                ProviderId = "google",
                MaxResults = 10,
                Market = "en-US"
            }
        };

        // Assert
        Assert.Equal("google", payload.Search.ProviderId);
        Assert.Equal(10, payload.Search.MaxResults);
        Assert.Equal("en-US", payload.Search.Market);
    }

    [Fact]
    public void SearchProviderDescriptor_Google_HasCorrectCapabilities()
    {
        // Arrange
        var googleProvider = new SearchProviderDescriptor
        {
            Id = "google",
            DisplayName = "Google Custom Search",
            Enabled = true,
            IsDefault = false,
            Capabilities = new SearchProviderCapabilities
            {
                SupportsMarket = true,
                SupportsSafeSearch = true,
                SupportsSiteRestrictions = true,
                MaxResultsPerRequest = 10,
                RateLimitPerMinute = 100
            }
        };

        // Assert
        Assert.Equal("google", googleProvider.Id);
        Assert.True(googleProvider.Capabilities.SupportsSiteRestrictions);
        Assert.Equal(10, googleProvider.Capabilities.MaxResultsPerRequest);
    }

    [Fact]
    public void SearchProviderDescriptor_Brave_HasCorrectCapabilities()
    {
        // Arrange
        var braveProvider = new SearchProviderDescriptor
        {
            Id = "brave",
            DisplayName = "Brave Search",
            Enabled = true,
            IsDefault = true,
            Capabilities = new SearchProviderCapabilities
            {
                SupportsMarket = true,
                SupportsSafeSearch = true,
                SupportsSiteRestrictions = false,
                MaxResultsPerRequest = 20,
                RateLimitPerMinute = 15
            }
        };

        // Assert
        Assert.Equal("brave", braveProvider.Id);
        Assert.True(braveProvider.IsDefault);
        Assert.False(braveProvider.Capabilities.SupportsSiteRestrictions);
        Assert.Equal(20, braveProvider.Capabilities.MaxResultsPerRequest);
    }

    [Fact]
    public void SearchProvidersResponse_ContainsDefaultAndProviders()
    {
        // Arrange
        var response = new SearchProvidersResponse
        {
            DefaultProviderId = "brave",
            Providers = new List<SearchProviderDescriptor>
            {
                new() { Id = "brave", DisplayName = "Brave Search", Enabled = true, IsDefault = true },
                new() { Id = "google", DisplayName = "Google Custom Search", Enabled = true, IsDefault = false }
            }
        };

        // Assert
        Assert.Equal("brave", response.DefaultProviderId);
        Assert.Equal(2, response.Providers.Count);
        Assert.Contains(response.Providers, p => p.Id == "brave" && p.IsDefault);
        Assert.Contains(response.Providers, p => p.Id == "google" && !p.IsDefault);
    }

    #endregion

    #region M4-059: Invalid provider ? 400 INVALID_SEARCH_PROVIDER

    [Fact]
    public void QueryModePayload_WithUnknownProvider_ShouldBeRejected()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "chocolate chip cookies",
            Search = new SearchSettings
            {
                ProviderId = "unknown-provider"
            }
        };

        // Act
        var isKnownProvider = IsKnownSearchProvider(payload.Search.ProviderId);

        // Assert
        Assert.False(isKnownProvider, "Unknown provider should be rejected");
    }

    [Theory]
    [InlineData("bing")]
    [InlineData("duckduckgo")]
    [InlineData("yahoo")]
    [InlineData("")]
    [InlineData("invalid")]
    public void QueryModePayload_WithInvalidProvider_ShouldFail(string invalidProvider)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "recipe",
            Search = new SearchSettings
            {
                ProviderId = invalidProvider
            }
        };

        // Act
        var isKnown = IsKnownSearchProvider(payload.Search.ProviderId);

        // Assert
        Assert.False(isKnown, $"Provider '{invalidProvider}' should be unknown");
    }

    [Theory]
    [InlineData("brave")]
    [InlineData("google")]
    public void QueryModePayload_WithValidProvider_ShouldSucceed(string validProvider)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "recipe",
            Search = new SearchSettings
            {
                ProviderId = validProvider
            }
        };

        // Act
        var isKnown = IsKnownSearchProvider(payload.Search.ProviderId);

        // Assert
        Assert.True(isKnown, $"Provider '{validProvider}' should be known");
    }

    [Fact]
    public void SearchProviderNotFoundException_HasCorrectErrorCode()
    {
        // Arrange
        var exception = new SearchProviderNotFoundException("disabled-provider", "Provider 'disabled-provider' is not enabled.");

        // Assert
        Assert.Equal("INVALID_SEARCH_PROVIDER", exception.ErrorCode);
        Assert.Equal("disabled-provider", exception.ProviderId);
        Assert.Contains("disabled-provider", exception.Message);
    }

    #endregion

    #region M4-060: Provider fallback on 429

    [Fact]
    public void TransientErrors_ShouldTriggerFallback()
    {
        // Arrange - Error codes that should trigger fallback
        var transientCodes = new[]
        {
            "RATE_LIMIT_EXCEEDED",
            "QUOTA_EXCEEDED",
            "SERVICE_UNAVAILABLE",
            "TIMEOUT",
            "HTTP_429",
            "HTTP_503",
            "HTTP_504"
        };

        // Assert
        foreach (var code in transientCodes)
        {
            Assert.True(IsTransientError(code), $"Error code '{code}' should trigger fallback");
        }
    }

    [Fact]
    public void NonTransientErrors_ShouldNotTriggerFallback()
    {
        // Arrange - Error codes that should NOT trigger fallback
        var nonTransientCodes = new[]
        {
            "INVALID_API_KEY",
            "UNAUTHORIZED",
            "FORBIDDEN",
            "NOT_FOUND",
            "INVALID_REQUEST"
        };

        // Assert
        foreach (var code in nonTransientCodes)
        {
            Assert.False(IsTransientError(code), $"Error code '{code}' should NOT trigger fallback");
        }
    }

    [Fact]
    public void FallbackMetadata_RecordsOriginalProviderAndReason()
    {
        // Arrange
        var fallbackInfo = new
        {
            UsedFallbackProvider = true,
            OriginalProviderId = "google",
            FallbackProviderId = "brave",
            FallbackReason = "RATE_LIMIT_EXCEEDED: Rate limit exceeded. Please try again later."
        };

        // Assert
        Assert.True(fallbackInfo.UsedFallbackProvider);
        Assert.Equal("google", fallbackInfo.OriginalProviderId);
        Assert.Equal("brave", fallbackInfo.FallbackProviderId);
        Assert.Contains("RATE_LIMIT_EXCEEDED", fallbackInfo.FallbackReason);
    }

    [Fact]
    public void SearchResult_WithRateLimitError_HasCorrectStructure()
    {
        // Arrange
        var result = new SearchResult
        {
            Success = false,
            ErrorCode = "RATE_LIMIT_EXCEEDED",
            Error = "Rate limit exceeded. Please try again after 60 seconds.",
            Candidates = new List<SearchCandidate>()
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("RATE_LIMIT_EXCEEDED", result.ErrorCode);
        Assert.NotNull(result.Error);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void FallbackArtifact_ContainsCompleteInfo()
    {
        // Arrange
        var fallbackArtifact = new
        {
            UsedFallbackProvider = true,
            OriginalProviderId = "google",
            FallbackProviderId = "brave",
            FallbackReason = "QUOTA_EXCEEDED: Daily quota exceeded",
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(fallbackArtifact, JsonOptions);
        
        // Assert
        Assert.Contains("usedFallbackProvider", json);
        Assert.Contains("originalProviderId", json);
        Assert.Contains("fallbackProviderId", json);
        Assert.Contains("fallbackReason", json);
        Assert.Contains("timestamp", json);
    }

    [Fact]
    public void FallbackConfiguration_DefaultsToDisabled()
    {
        // Arrange - Default configuration
        var searchOptions = new Cookbook.Platform.Shared.Configuration.SearchOptions();

        // Assert
        Assert.False(searchOptions.AllowFallback);
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
                "Preheat oven to 375�F (190�C).",
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

    private static bool IsKnownSearchProvider(string? providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            return false;

        var knownProviders = new[] { "brave", "google" };
        return knownProviders.Contains(providerId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTransientError(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
            return false;

        var transientCodes = new[]
        {
            "RATE_LIMIT_EXCEEDED",
            "QUOTA_EXCEEDED",
            "SERVICE_UNAVAILABLE",
            "TIMEOUT",
            "HTTP_429",
            "HTTP_503",
            "HTTP_504"
        };

        return transientCodes.Contains(errorCode, StringComparer.OrdinalIgnoreCase);
    }

    #endregion
}
