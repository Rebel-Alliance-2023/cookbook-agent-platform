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
/// Unit tests for BraveSearchProvider.
/// </summary>
public class BraveSearchProviderTests : IDisposable
{
    private readonly Mock<ILogger<BraveSearchProvider>> _loggerMock;
    private readonly BraveSearchOptions _options;
    private BraveSearchProvider? _provider;

    public BraveSearchProviderTests()
    {
        _loggerMock = new Mock<ILogger<BraveSearchProvider>>();
        _options = new BraveSearchOptions
        {
            ApiKey = "test-api-key",
            Endpoint = "https://api.search.brave.com/res/v1/web/search",
            Enabled = true,
            IsDefault = true,
            MaxResults = 10,
            Market = "en-US",
            SafeSearch = "moderate",
            RateLimitPerMinute = 100 // High limit for tests
        };
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }

    private BraveSearchProvider CreateProvider(HttpClient httpClient)
    {
        _provider = new BraveSearchProvider(
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
            BaseAddress = new Uri("https://api.search.brave.com/res/v1/web/search")
        };
    }

    #region Provider Properties Tests

    [Fact]
    public void ProviderId_ReturnsExpectedValue()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.Equal("brave", provider.ProviderId);
    }

    [Fact]
    public void DisplayName_ReturnsExpectedValue()
    {
        var httpClient = CreateMockHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(httpClient);

        Assert.Equal("Brave Search", provider.DisplayName);
    }

    [Fact]
    public void IsEnabled_WhenConfiguredAndHasApiKey_ReturnsTrue()
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
        Assert.Equal("brave", result.ProviderId);
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
        var braveResponse = new
        {
            query = new { original = "chocolate chip cookies", total_count = 100 },
            web = new
            {
                results = new[]
                {
                    new { url = "https://example.com/recipe1", title = "Best Cookies", description = "Delicious recipe" },
                    new { url = "https://example.com/recipe2", title = "Easy Cookies", description = "Simple recipe" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
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
        Assert.Equal(0, result.Candidates[0].Position);
        Assert.Equal(1, result.Candidates[1].Position);
        Assert.Equal("brave", result.ProviderId);
    }

    [Fact]
    public async Task SearchAsync_ExtractsSiteNameFromUrl()
    {
        var braveResponse = new
        {
            query = new { original = "test" },
            web = new
            {
                results = new[]
                {
                    new { url = "https://www.allrecipes.com/recipe/123", title = "Test", description = "Test" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Equal("allrecipes.com", result.Candidates[0].SiteName);
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
    public async Task SearchAsync_When402Response_ReturnsQuotaExceeded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PaymentRequired);
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

    #region Domain Filtering Tests

    [Fact]
    public async Task SearchAsync_WhenDeniedDomain_FiltersOutResult()
    {
        _options.DeniedDomains = ["blocked.com"];

        var braveResponse = new
        {
            query = new { original = "test" },
            web = new
            {
                results = new[]
                {
                    new { url = "https://allowed.com/recipe", title = "Allowed", description = "OK" },
                    new { url = "https://blocked.com/recipe", title = "Blocked", description = "No" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
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

        var braveResponse = new
        {
            query = new { original = "test" },
            web = new
            {
                results = new[]
                {
                    new { url = "https://approved.com/recipe", title = "Approved", description = "OK" },
                    new { url = "https://other.com/recipe", title = "Other", description = "No" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal("https://approved.com/recipe", result.Candidates[0].Url);
    }

    [Fact]
    public async Task SearchAsync_DenyListTakesPrecedenceOverAllowList()
    {
        _options.AllowedDomains = ["example.com"];
        _options.DeniedDomains = ["example.com"]; // Same domain in both lists

        var braveResponse = new
        {
            query = new { original = "test" },
            web = new
            {
                results = new[]
                {
                    new { url = "https://example.com/recipe", title = "Test", description = "Test" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Empty(result.Candidates); // Denied takes precedence
    }

    #endregion

    #region Empty/Null Response Handling Tests

    [Fact]
    public async Task SearchAsync_WhenEmptyResults_ReturnsEmptyCandidates()
    {
        var braveResponse = new
        {
            query = new { original = "test" },
            web = new { results = Array.Empty<object>() }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
        };

        var httpClient = CreateMockHttpClient(response);
        var provider = CreateProvider(httpClient);

        var request = new SearchRequest { Query = "test" };
        var result = await provider.SearchAsync(request);

        Assert.True(result.Success);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task SearchAsync_SkipsResultsWithNullUrl()
    {
        var braveResponse = new
        {
            query = new { original = "test" },
            web = new
            {
                results = new object[]
                {
                    new { url = (string?)null, title = "No URL", description = "Test" },
                    new { url = "https://valid.com/recipe", title = "Valid", description = "Test" }
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(braveResponse))
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
