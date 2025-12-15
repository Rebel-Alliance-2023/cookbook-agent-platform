using System.Net;
using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest.Search;

/// <summary>
/// Unit tests for GoogleCustomSearchProvider.
/// </summary>
public class GoogleCustomSearchProviderTests : IDisposable
{
    private readonly Mock<ILogger<GoogleCustomSearchProvider>> _loggerMock;
    private readonly GoogleSearchOptions _options;
    private GoogleCustomSearchProvider? _provider;

    public GoogleCustomSearchProviderTests()
    {
        _loggerMock = new Mock<ILogger<GoogleCustomSearchProvider>>();
        _options = new GoogleSearchOptions
        {
            ApiKey = "test-api-key",
            SearchEngineId = "test-cx-id",
            Endpoint = "https://www.googleapis.com/customsearch/v1",
            Enabled = true,
            IsDefault = false,
            MaxResults = 10,
            Language = "en",
            Country = "us",
            SafeSearch = "medium",
            RateLimitPerMinute = 100 // High limit for tests
        };
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }

    private GoogleCustomSearchProvider CreateProvider(HttpClient httpClient)
    {
        _provider = new GoogleCustomSearchProvider(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);
        return _provider;
    }

    private static HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://www.googleapis.com/customsearch/v1")
        };
    }

    #region Provider Properties Tests

    [Fact]
    public void ProviderId_ReturnsExpectedValue()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.Equal("google", provider.ProviderId);
    }

    [Fact]
    public void DisplayName_ReturnsExpectedValue()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.Equal("Google Custom Search", provider.DisplayName);
    }

    [Fact]
    public void IsEnabled_WhenConfiguredAndHasCredentials_ReturnsTrue()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.True(provider.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenDisabledInConfig_ReturnsFalse()
    {
        _options.Enabled = false;
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenNoApiKey_ReturnsFalse()
    {
        _options.ApiKey = string.Empty;
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenNoSearchEngineId_ReturnsFalse()
    {
        _options.SearchEngineId = string.Empty;
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.False(provider.IsEnabled);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_WhenDisabled_ReturnsProviderDisabledError()
    {
        _options.Enabled = false;
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "chocolate chip cookies recipe" };
        var result = await provider.SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal("PROVIDER_DISABLED", result.ErrorCode);
        Assert.Equal("google", result.ProviderId);
    }

    [Fact]
    public async Task SearchAsync_WhenEmptyQuery_ReturnsInvalidQueryError()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "" };
        var result = await provider.SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal("INVALID_QUERY", result.ErrorCode);
    }

    [Fact]
    public async Task SearchAsync_WhenSuccessfulResponse_ReturnsCandidates()
    {
        var googleResponse = new
        {
            kind = "customsearch#search",
            searchInformation = new { totalResults = "1000", searchTime = 0.5 },
            items = new[]
            {
                new { link = "https://example.com/recipe1", title = "Best Cookies", snippet = "Delicious recipe", displayLink = "example.com" },
                new { link = "https://example.com/recipe2", title = "Easy Cookies", snippet = "Simple recipe", displayLink = "example.com" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "chocolate chip cookies recipe" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("https://example.com/recipe1", result.Candidates[0].Url);
        Assert.Equal("Best Cookies", result.Candidates[0].Title);
        Assert.Equal("Delicious recipe", result.Candidates[0].Snippet);
        Assert.Equal("example.com", result.Candidates[0].SiteName);
        Assert.Equal(0, result.Candidates[0].Position);
        Assert.Equal(1, result.Candidates[1].Position);
        Assert.Equal("google", result.ProviderId);
        Assert.Equal(1000, result.TotalResults);
    }

    [Fact]
    public async Task SearchAsync_UsesDisplayLinkFromResponse()
    {
        var googleResponse = new
        {
            searchInformation = new { totalResults = "1" },
            items = new[]
            {
                new { link = "https://www.allrecipes.com/recipe/123", title = "Test", snippet = "Test", displayLink = "www.allrecipes.com" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Equal("www.allrecipes.com", result.Candidates[0].SiteName);
    }

    [Fact]
    public async Task SearchAsync_When429Response_ReturnsRateLimited()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal("RATE_LIMITED", result.ErrorCode);
    }

    [Fact]
    public async Task SearchAsync_When403WithQuotaMessage_ReturnsQuotaExceeded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\": {\"message\": \"Daily Limit Exceeded. The quota will be reset at midnight Pacific Time.\"}}")
        };
        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal("QUOTA_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task SearchAsync_WhenServerError_ReturnsHttpError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.False(result.Success);
        Assert.Equal("HTTP_500", result.ErrorCode);
    }

    #endregion

    #region Site Restriction Tests

    [Fact]
    public async Task SearchAsync_WhenSiteRestrictionsEnabled_BuildsCorrectQuery()
    {
        _options.UseSiteRestrictions = true;
        _options.SiteRestrictions = ["allrecipes.com", "foodnetwork.com"];

        string? capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedUri = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\": []}")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://www.googleapis.com/customsearch/v1")
        };

        var provider = CreateProvider(httpClient);
        var request = new SearchRequest { Query = "cookies recipe" };
        await provider.SearchAsync(request);

        Assert.NotNull(capturedUri);
        // The query should contain site: operators
        Assert.Contains("site%3Aallrecipes.com", capturedUri);
        Assert.Contains("site%3Afoodnetwork.com", capturedUri);
    }

    #endregion

    #region Domain Filtering Tests

    [Fact]
    public async Task SearchAsync_WhenDeniedDomain_FiltersOutResult()
    {
        _options.DeniedDomains = ["blocked.com"];

        var googleResponse = new
        {
            searchInformation = new { totalResults = "2" },
            items = new[]
            {
                new { link = "https://allowed.com/recipe", title = "Allowed", snippet = "OK", displayLink = "allowed.com" },
                new { link = "https://blocked.com/recipe", title = "Blocked", snippet = "No", displayLink = "blocked.com" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal("https://allowed.com/recipe", result.Candidates[0].Url);
    }

    [Fact]
    public async Task SearchAsync_WhenAllowedDomainsSet_OnlyIncludesAllowedDomains()
    {
        _options.AllowedDomains = ["approved.com"];

        var googleResponse = new
        {
            searchInformation = new { totalResults = "2" },
            items = new[]
            {
                new { link = "https://approved.com/recipe", title = "Approved", snippet = "OK", displayLink = "approved.com" },
                new { link = "https://other.com/recipe", title = "Other", snippet = "No", displayLink = "other.com" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal("https://approved.com/recipe", result.Candidates[0].Url);
    }

    #endregion

    #region Empty/Null Response Handling Tests

    [Fact]
    public async Task SearchAsync_WhenEmptyResults_ReturnsEmptyCandidates()
    {
        var googleResponse = new
        {
            searchInformation = new { totalResults = "0" },
            items = Array.Empty<object>()
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task SearchAsync_WhenNoItemsProperty_ReturnsEmptyCandidates()
    {
        var googleResponse = new
        {
            searchInformation = new { totalResults = "0" }
            // No items property
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task SearchAsync_SkipsResultsWithNullLink()
    {
        var googleResponse = new
        {
            searchInformation = new { totalResults = "2" },
            items = new object[]
            {
                new { link = (string?)null, title = "No Link", snippet = "Test", displayLink = "test.com" },
                new { link = "https://valid.com/recipe", title = "Valid", snippet = "Test", displayLink = "valid.com" }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(googleResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal("https://valid.com/recipe", result.Candidates[0].Url);
    }

    #endregion
}
