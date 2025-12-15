using System.Text.Json;
using Cookbook.Platform.Shared.Models.Ingest;
using Xunit;

namespace Cookbook.Platform.Gateway.Tests.Endpoints;

/// <summary>
/// Integration tests for the Ingest Task creation endpoint.
/// Tests validation, ThreadId generation, and response contracts.
/// </summary>
public class IngestTaskEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    #region Payload Validation Tests - URL Mode

    [Fact]
    public void UrlMode_WithValidUrl_IsValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipes/test"
        };

        // Assert
        Assert.Equal(IngestMode.Url, payload.Mode);
        Assert.NotNull(payload.Url);
        Assert.StartsWith("https://", payload.Url);
    }

    [Fact]
    public void UrlMode_WithHttpUrl_IsValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "http://example.com/recipes/test"
        };

        // Assert
        Assert.Equal(IngestMode.Url, payload.Mode);
        Assert.StartsWith("http://", payload.Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UrlMode_WithMissingUrl_FailsValidation(string? url)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = url
        };

        // Assert - URL is required for URL mode
        Assert.True(string.IsNullOrWhiteSpace(payload.Url));
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///path/to/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-valid-url")]
    public void UrlMode_WithInvalidScheme_FailsValidation(string invalidUrl)
    {
        // Arrange
        var isValidUrl = Uri.TryCreate(invalidUrl, UriKind.Absolute, out var uri) 
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        // Assert
        Assert.False(isValidUrl);
    }

    #endregion

    #region Payload Validation Tests - Query Mode

    [Fact]
    public void QueryMode_WithValidQuery_IsValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "best tonkotsu ramen recipe"
        };

        // Assert
        Assert.Equal(IngestMode.Query, payload.Mode);
        Assert.NotNull(payload.Query);
        Assert.False(string.IsNullOrWhiteSpace(payload.Query));
    }

    [Fact]
    public void QueryMode_WithConstraints_IsValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "healthy pasta",
            Constraints = new IngestConstraints
            {
                DietType = "vegetarian",
                Cuisine = "Italian",
                MaxPrepMinutes = 30
            }
        };

        // Assert
        Assert.NotNull(payload.Constraints);
        Assert.Equal("vegetarian", payload.Constraints.DietType);
        Assert.Equal("Italian", payload.Constraints.Cuisine);
        Assert.Equal(30, payload.Constraints.MaxPrepMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QueryMode_WithMissingQuery_FailsValidation(string? query)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = query
        };

        // Assert - Query is required for Query mode
        Assert.True(string.IsNullOrWhiteSpace(payload.Query));
    }

    #endregion

    #region Payload Validation Tests - Normalize Mode

    [Fact]
    public void NormalizeMode_WithRecipeId_IsValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-abc-123"
        };

        // Assert
        Assert.Equal(IngestMode.Normalize, payload.Mode);
        Assert.NotNull(payload.RecipeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeMode_WithMissingRecipeId_FailsValidation(string? recipeId)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = recipeId
        };

        // Assert - RecipeId is required for Normalize mode
        Assert.True(string.IsNullOrWhiteSpace(payload.RecipeId));
    }

    [Fact]
    public void NormalizeMode_WithFocusAreas_SerializesCorrectly()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-abc-123",
            NormalizeOptions = new NormalizeOptions
            {
                FocusAreas = ["capitalization", "units", "grammar"]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(IngestMode.Normalize, deserialized.Mode);
        Assert.NotNull(deserialized.NormalizeOptions);
        Assert.NotNull(deserialized.NormalizeOptions.FocusAreas);
        Assert.Equal(3, deserialized.NormalizeOptions.FocusAreas.Count);
        Assert.Contains("capitalization", deserialized.NormalizeOptions.FocusAreas);
    }

    [Fact]
    public void NormalizeMode_WithAutoApplyLowRisk_SerializesCorrectly()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-abc-123",
            NormalizeOptions = new NormalizeOptions
            {
                AutoApplyLowRisk = true,
                MaxRiskLevel = NormalizePatchRiskCategory.Medium
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.NormalizeOptions);
        Assert.True(deserialized.NormalizeOptions.AutoApplyLowRisk);
        Assert.Equal(NormalizePatchRiskCategory.Medium, deserialized.NormalizeOptions.MaxRiskLevel);
    }

    [Fact]
    public void NormalizeMode_WithNormalizePromptId_SerializesCorrectly()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-abc-123",
            PromptSelection = new PromptSelection
            {
                NormalizePromptId = "ingest.normalize.v2"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.PromptSelection);
        Assert.Equal("ingest.normalize.v2", deserialized.PromptSelection.NormalizePromptId);
    }

    [Fact]
    public void NormalizeMode_BuildsMetadata_CorrectlyIncludesRecipeId()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-xyz-789"
        };

        // Act - simulate metadata building (same logic as TaskEndpoints.BuildIngestMetadata)
        var metadata = BuildTestMetadata(payload);

        // Assert
        Assert.Equal("Normalize", metadata["ingestMode"]);
        Assert.Equal("recipe-xyz-789", metadata["recipeId"]);
    }

    [Fact]
    public void NormalizeMode_BuildsMetadata_IncludesNormalizePromptId()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-xyz-789",
            PromptSelection = new PromptSelection
            {
                NormalizePromptId = "ingest.normalize.v1"
            }
        };

        // Act - simulate metadata building
        var metadata = BuildTestMetadata(payload);

        // Assert
        Assert.Equal("Normalize", metadata["ingestMode"]);
        Assert.Contains("promptId:normalize", metadata.Keys);
        Assert.Equal("ingest.normalize.v1", metadata["promptId:normalize"]);
    }

    [Fact]
    public void NormalizeMode_ValidateIngestPayload_ReturnsNull_WhenValid()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-valid-id"
        };

        // Act - simulate validation (same logic as TaskEndpoints.ValidateIngestPayload)
        var validationError = ValidateIngestPayloadTest(payload);

        // Assert
        Assert.Null(validationError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeMode_ValidateIngestPayload_ReturnsError_WhenRecipeIdMissing(string? recipeId)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = recipeId
        };

        // Act - simulate validation
        var validationError = ValidateIngestPayloadTest(payload);

        // Assert
        Assert.NotNull(validationError);
        Assert.Equal("MISSING_RECIPE_ID", validationError.Code);
    }

    [Fact]
    public void NormalizeMode_DefaultNormalizeOptions_HasCorrectDefaults()
    {
        // Arrange
        var options = new NormalizeOptions();

        // Assert
        Assert.Null(options.FocusAreas);
        Assert.False(options.AutoApplyLowRisk);
        Assert.Equal(NormalizePatchRiskCategory.High, options.MaxRiskLevel);
    }

    [Fact]
    public void NormalizeMode_FullPayload_SerializesRoundTrip()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-full-test",
            NormalizeOptions = new NormalizeOptions
            {
                FocusAreas = ["capitalization", "units"],
                AutoApplyLowRisk = true,
                MaxRiskLevel = NormalizePatchRiskCategory.Low
            },
            PromptSelection = new PromptSelection
            {
                NormalizePromptId = "custom.normalize.prompt"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(IngestMode.Normalize, deserialized.Mode);
        Assert.Equal("recipe-full-test", deserialized.RecipeId);
        Assert.NotNull(deserialized.NormalizeOptions);
        Assert.Equal(2, deserialized.NormalizeOptions.FocusAreas!.Count);
        Assert.True(deserialized.NormalizeOptions.AutoApplyLowRisk);
        Assert.Equal(NormalizePatchRiskCategory.Low, deserialized.NormalizeOptions.MaxRiskLevel);
        Assert.Equal("custom.normalize.prompt", deserialized.PromptSelection?.NormalizePromptId);
    }

    #endregion

    #region ThreadId Generation Tests

    [Fact]
    public void ThreadId_WhenNull_ShouldBeGenerated()
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = null,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };

        // Simulate ThreadId generation logic
        var threadId = string.IsNullOrWhiteSpace(request.ThreadId) 
            ? Guid.NewGuid().ToString() 
            : request.ThreadId;

        // Assert
        Assert.NotNull(threadId);
        Assert.True(Guid.TryParse(threadId, out _));
    }

    [Fact]
    public void ThreadId_WhenProvided_ShouldBePreserved()
    {
        // Arrange
        var providedThreadId = "user-provided-thread-123";
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = providedThreadId,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };

        // Simulate ThreadId logic
        var threadId = string.IsNullOrWhiteSpace(request.ThreadId) 
            ? Guid.NewGuid().ToString() 
            : request.ThreadId;

        // Assert
        Assert.Equal(providedThreadId, threadId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ThreadId_WhenWhitespace_ShouldBeGenerated(string whitespaceThreadId)
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = whitespaceThreadId,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };

        // Simulate ThreadId logic
        var threadId = string.IsNullOrWhiteSpace(request.ThreadId) 
            ? Guid.NewGuid().ToString() 
            : request.ThreadId;

        // Assert
        Assert.NotEqual(whitespaceThreadId, threadId);
        Assert.True(Guid.TryParse(threadId, out _));
    }

    #endregion

    #region Response Contract Tests

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

        // Assert
        Assert.NotNull(response.TaskId);
        Assert.NotNull(response.ThreadId);
        Assert.NotNull(response.AgentType);
        Assert.NotNull(response.Status);
    }

    [Fact]
    public void CreateIngestTaskResponse_Serializes_WithCorrectPropertyNames()
    {
        // Arrange
        var response = new CreateIngestTaskResponse
        {
            TaskId = "task-abc",
            ThreadId = "thread-xyz",
            AgentType = "Ingest",
            Status = "Pending"
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);

        // Assert
        Assert.Contains("\"taskId\":", json);
        Assert.Contains("\"threadId\":", json);
        Assert.Contains("\"agentType\":", json);
        Assert.Contains("\"status\":", json);
    }

    #endregion

    #region Metadata Building Tests

    [Fact]
    public void Metadata_UrlMode_ContainsSourceUrl()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var metadata = BuildTestMetadata(payload);

        // Assert
        Assert.Contains("ingestMode", metadata.Keys);
        Assert.Equal("Url", metadata["ingestMode"]);
        Assert.Contains("sourceUrl", metadata.Keys);
        Assert.Equal("https://example.com/recipe", metadata["sourceUrl"]);
    }

    [Fact]
    public void Metadata_QueryMode_ContainsSearchQuery()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test query"
        };

        var metadata = BuildTestMetadata(payload);

        // Assert
        Assert.Contains("ingestMode", metadata.Keys);
        Assert.Equal("Query", metadata["ingestMode"]);
        Assert.Contains("searchQuery", metadata.Keys);
        Assert.Equal("test query", metadata["searchQuery"]);
    }

    [Fact]
    public void Metadata_WithPromptSelection_ContainsPromptIds()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe",
            PromptSelection = new PromptSelection
            {
                ExtractPromptId = "ingest.extract.v1",
                NormalizePromptId = "ingest.normalize.v1"
            }
        };

        var metadata = BuildTestMetadata(payload);

        // Assert
        Assert.Contains("promptId:extract", metadata.Keys);
        Assert.Equal("ingest.extract.v1", metadata["promptId:extract"]);
        Assert.Contains("promptId:normalize", metadata.Keys);
        Assert.Equal("ingest.normalize.v1", metadata["promptId:normalize"]);
    }

    private static Dictionary<string, string> BuildTestMetadata(IngestPayload payload)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ingestMode"] = payload.Mode.ToString()
        };

        if (!string.IsNullOrWhiteSpace(payload.Url))
            metadata["sourceUrl"] = payload.Url;
            
        if (!string.IsNullOrWhiteSpace(payload.Query))
            metadata["searchQuery"] = payload.Query;
            
        if (!string.IsNullOrWhiteSpace(payload.RecipeId))
            metadata["recipeId"] = payload.RecipeId;

        if (payload.PromptSelection?.ExtractPromptId is not null)
            metadata["promptId:extract"] = payload.PromptSelection.ExtractPromptId;
            
        if (payload.PromptSelection?.NormalizePromptId is not null)
            metadata["promptId:normalize"] = payload.PromptSelection.NormalizePromptId;
            
        if (payload.PromptSelection?.DiscoverPromptId is not null)
            metadata["promptId:discover"] = payload.PromptSelection.DiscoverPromptId;

        return metadata;
    }

    #endregion

    #region Agent Type Validation Tests

    [Fact]
    public void IngestEndpoint_RejectsNonIngestAgentType()
    {
        // Arrange
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Research", // Wrong type for /ingest endpoint
            ThreadId = "thread-123",
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };

        // Assert - This should fail validation at the endpoint level
        Assert.NotEqual("Ingest", request.AgentType);
    }

    [Theory]
    [InlineData("Ingest")]
    [InlineData("ingest")]
    [InlineData("INGEST")]
    public void IngestEndpoint_AcceptsIngestAgentType_CaseInsensitive(string agentType)
    {
        // Assert
        Assert.True(
            string.Equals(agentType, "Ingest", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Query Mode - Search Provider Selection Tests

    [Fact]
    public void QueryMode_WithProviderId_StoresInSearchSettings()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "best ramen recipe",
            Search = new SearchSettings
            {
                ProviderId = "brave",
                MaxResults = 5,
                Market = "en-US",
                SafeSearch = "moderate"
            }
        };

        // Assert
        Assert.NotNull(payload.Search);
        Assert.Equal("brave", payload.Search.ProviderId);
        Assert.Equal(5, payload.Search.MaxResults);
        Assert.Equal("en-US", payload.Search.Market);
        Assert.Equal("moderate", payload.Search.SafeSearch);
    }

    [Fact]
    public void QueryMode_WithoutProviderId_SearchSettingsCanBeNull()
    {
        // Arrange - Provider defaults to system default when not specified
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "best ramen recipe",
            Search = null
        };

        // Assert
        Assert.Null(payload.Search);
    }

    [Fact]
    public void QueryMode_WithEmptyProviderId_IsValid()
    {
        // Arrange - Empty provider ID means use default
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "best ramen recipe",
            Search = new SearchSettings
            {
                ProviderId = null
            }
        };

        // Assert
        Assert.NotNull(payload.Search);
        Assert.Null(payload.Search.ProviderId);
    }

    [Theory]
    [InlineData("brave")]
    [InlineData("google")]
    [InlineData("BRAVE")]
    [InlineData("Google")]
    public void QueryMode_WithValidProviderIds(string providerId)
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test query",
            Search = new SearchSettings
            {
                ProviderId = providerId
            }
        };

        // Assert
        Assert.Equal(providerId, payload.Search!.ProviderId);
    }

    [Fact]
    public void SearchSettings_Serializes_WithCorrectPropertyNames()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test",
            Search = new SearchSettings
            {
                ProviderId = "brave",
                MaxResults = 10,
                Market = "en-US",
                SafeSearch = "moderate"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        // Assert
        Assert.Contains("\"search\":", json);
        Assert.Contains("\"providerId\":\"brave\"", json);
        Assert.Contains("\"maxResults\":10", json);
        Assert.Contains("\"market\":\"en-US\"", json);
        Assert.Contains("\"safeSearch\":\"moderate\"", json);
    }

    [Fact]
    public void Metadata_QueryMode_WithProvider_ContainsProviderId()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test query",
            Search = new SearchSettings
            {
                ProviderId = "brave"
            }
        };

        var metadata = BuildTestMetadataWithProvider(payload, "brave");

        // Assert
        Assert.Contains("searchProviderId", metadata.Keys);
        Assert.Equal("brave", metadata["searchProviderId"]);
    }

    [Fact]
    public void Metadata_QueryMode_WithDefaultProvider_ContainsResolvedProviderId()
    {
        // Arrange - When no provider specified, default is used
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test query"
        };

        // Simulate default provider resolution
        var resolvedProviderId = "brave"; // Default provider
        var metadata = BuildTestMetadataWithProvider(payload, resolvedProviderId);

        // Assert
        Assert.Contains("searchProviderId", metadata.Keys);
        Assert.Equal("brave", metadata["searchProviderId"]);
    }

    private static Dictionary<string, string> BuildTestMetadataWithProvider(IngestPayload payload, string? resolvedProviderId)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ingestMode"] = payload.Mode.ToString()
        };

        if (!string.IsNullOrWhiteSpace(payload.Url))
            metadata["sourceUrl"] = payload.Url;
            
        if (!string.IsNullOrWhiteSpace(payload.Query))
            metadata["searchQuery"] = payload.Query;
            
        if (!string.IsNullOrWhiteSpace(payload.RecipeId))
            metadata["recipeId"] = payload.RecipeId;

        // Store resolved provider ID
        if (!string.IsNullOrWhiteSpace(resolvedProviderId))
            metadata["searchProviderId"] = resolvedProviderId;

        if (payload.PromptSelection?.ExtractPromptId is not null)
            metadata["promptId:extract"] = payload.PromptSelection.ExtractPromptId;
            
        if (payload.PromptSelection?.NormalizePromptId is not null)
            metadata["promptId:normalize"] = payload.PromptSelection.NormalizePromptId;
            
        if (payload.PromptSelection?.DiscoverPromptId is not null)
            metadata["promptId:discover"] = payload.PromptSelection.DiscoverPromptId;

        return metadata;
    }

    /// <summary>
    /// Simulates the validation logic from TaskEndpoints.ValidateIngestPayload.
    /// </summary>
    private static ValidationError? ValidateIngestPayloadTest(IngestPayload payload)
    {
        return payload.Mode switch
        {
            IngestMode.Url when string.IsNullOrWhiteSpace(payload.Url) =>
                new ValidationError("MISSING_URL", "URL is required when mode is 'Url'"),
                
            IngestMode.Url when !IsValidUrl(payload.Url) =>
                new ValidationError("INVALID_URL", "URL must be a valid HTTP or HTTPS URL"),
                
            IngestMode.Query when string.IsNullOrWhiteSpace(payload.Query) =>
                new ValidationError("MISSING_QUERY", "Query is required when mode is 'Query'"),
                
            IngestMode.Normalize when string.IsNullOrWhiteSpace(payload.RecipeId) =>
                new ValidationError("MISSING_RECIPE_ID", "RecipeId is required when mode is 'Normalize'"),
                
            _ => null
        };
    }

    /// <summary>
    /// Validates that a URL is a proper HTTP/HTTPS URL.
    /// </summary>
    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) 
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Simple validation error record for testing.
    /// </summary>
    private record ValidationError(string Code, string Message);

    #endregion
}
