using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for HttpFetchService URL validation.
/// </summary>
public class HttpFetchServiceValidationTests
{
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<ISsrfProtectionService> _ssrfMock;
    private readonly Mock<ICircuitBreakerService> _circuitBreakerMock;
    private readonly Mock<ILogger<HttpFetchService>> _loggerMock;
    private readonly IngestOptions _options;

    public HttpFetchServiceValidationTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _ssrfMock = new Mock<ISsrfProtectionService>();
        _circuitBreakerMock = new Mock<ICircuitBreakerService>();
        _loggerMock = new Mock<ILogger<HttpFetchService>>();
        _options = new IngestOptions();
    }

    private HttpFetchService CreateService()
    {
        var httpClient = new HttpClient();
        var options = Options.Create(_options);
        return new HttpFetchService(
            httpClient,
            _ssrfMock.Object,
            _circuitBreakerMock.Object,
            options,
            _loggerMock.Object);
    }

    #region URL Validation Tests

    [Theory]
    [InlineData("https://example.com/recipe", true)]
    [InlineData("http://example.com/recipe", true)]
    [InlineData("HTTP://EXAMPLE.COM/", true)]
    [InlineData("HTTPS://EXAMPLE.COM/path", true)]
    public void ValidateUrl_ValidHttpUrls_ReturnsValid(string url, bool expectedValid)
    {
        var service = CreateService();
        
        var (isValid, errorCode, errorMessage) = service.ValidateUrl(url);
        
        Assert.Equal(expectedValid, isValid);
        Assert.Null(errorCode);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateUrl_EmptyUrl_ReturnsInvalid(string? url)
    {
        var service = CreateService();
        
        var (isValid, errorCode, errorMessage) = service.ValidateUrl(url!);
        
        Assert.False(isValid);
        Assert.Equal("EMPTY_URL", errorCode);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com/path")]
    [InlineData("://missing-scheme.com")]
    public void ValidateUrl_InvalidFormat_ReturnsInvalid(string url)
    {
        var service = CreateService();
        
        var (isValid, errorCode, errorMessage) = service.ValidateUrl(url);
        
        Assert.False(isValid);
        Assert.Equal("INVALID_URL_FORMAT", errorCode);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>test</h1>")]
    [InlineData("mailto:test@example.com")]
    [InlineData("tel:+1234567890")]
    public void ValidateUrl_InvalidScheme_ReturnsInvalid(string url)
    {
        var service = CreateService();
        
        var (isValid, errorCode, errorMessage) = service.ValidateUrl(url);
        
        Assert.False(isValid);
        Assert.Equal("INVALID_SCHEME", errorCode);
        Assert.Contains("Only HTTP and HTTPS", errorMessage);
    }

    [Theory]
    [InlineData("http://user:pass@example.com/")]
    [InlineData("https://admin:secret@example.com/path")]
    public void ValidateUrl_CredentialsInUrl_ReturnsInvalid(string url)
    {
        var service = CreateService();
        
        var (isValid, errorCode, errorMessage) = service.ValidateUrl(url);
        
        Assert.False(isValid);
        Assert.Equal("CREDENTIALS_IN_URL", errorCode);
    }

    #endregion

    #region Fetch Integration Tests

    [Fact]
    public async Task FetchAsync_CircuitBreakerOpen_ReturnsBlockedResult()
    {
        var service = CreateService();
        var url = "https://failing.com/recipe";
        
        _circuitBreakerMock.Setup(x => x.IsAllowed(It.IsAny<string>())).Returns(false);
        
        var result = await service.FetchAsync(url);
        
        Assert.False(result.Success);
        Assert.True(result.WasBlockedByCircuitBreaker);
        Assert.Equal("CIRCUIT_BREAKER_OPEN", result.ErrorCode);
    }

    [Fact]
    public async Task FetchAsync_SsrfBlocked_ReturnsBlockedResult()
    {
        var service = CreateService();
        var url = "https://internal.example.com/";
        
        _circuitBreakerMock.Setup(x => x.IsAllowed(It.IsAny<string>())).Returns(true);
        _ssrfMock.Setup(x => x.ValidateUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SsrfValidationResult.Blocked("Private IP detected"));
        
        var result = await service.FetchAsync(url);
        
        Assert.False(result.Success);
        Assert.True(result.WasBlockedBySsrf);
        Assert.Equal("SSRF_BLOCKED", result.ErrorCode);
        
        // Should record failure
        _circuitBreakerMock.Verify(x => x.RecordFailure(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_InvalidUrl_ReturnsError()
    {
        var service = CreateService();
        var url = "not-a-valid-url";
        
        var result = await service.FetchAsync(url);
        
        Assert.False(result.Success);
        Assert.Equal("INVALID_URL_FORMAT", result.ErrorCode);
    }

    [Fact]
    public async Task FetchAsync_InvalidScheme_ReturnsError()
    {
        var service = CreateService();
        var url = "ftp://example.com/file";
        
        var result = await service.FetchAsync(url);
        
        Assert.False(result.Success);
        Assert.Equal("INVALID_SCHEME", result.ErrorCode);
    }

    #endregion
}

/// <summary>
/// Tests for FetchService interface contracts.
/// </summary>
public class FetchServiceContractTests
{
    [Fact]
    public void IFetchService_DefinesFetchAsync()
    {
        // Verify interface has the expected method
        var method = typeof(IFetchService).GetMethod("FetchAsync");
        
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<FetchResult>), method.ReturnType);
    }

    [Fact]
    public void IFetchService_DefinesValidateUrl()
    {
        var method = typeof(IFetchService).GetMethod("ValidateUrl");
        
        Assert.NotNull(method);
    }

    [Fact]
    public void FetchResult_HasRequiredProperties()
    {
        var properties = typeof(FetchResult).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();
        
        Assert.Contains("Success", propertyNames);
        Assert.Contains("Content", propertyNames);
        Assert.Contains("StatusCode", propertyNames);
        Assert.Contains("ContentType", propertyNames);
        Assert.Contains("Error", propertyNames);
        Assert.Contains("ErrorCode", propertyNames);
        Assert.Contains("WasBlockedBySsrf", propertyNames);
        Assert.Contains("WasBlockedByCircuitBreaker", propertyNames);
    }
}
