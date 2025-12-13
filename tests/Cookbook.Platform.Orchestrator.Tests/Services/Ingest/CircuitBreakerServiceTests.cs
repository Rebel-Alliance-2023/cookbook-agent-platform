using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for CircuitBreakerService.
/// </summary>
public class CircuitBreakerServiceTests
{
    private readonly Mock<ILogger<CircuitBreakerService>> _loggerMock;
    private readonly IngestOptions _options;

    public CircuitBreakerServiceTests()
    {
        _loggerMock = new Mock<ILogger<CircuitBreakerService>>();
        _options = new IngestOptions
        {
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = 3,
                FailureWindowMinutes = 1,
                BlockDurationMinutes = 1
            }
        };
    }

    private CircuitBreakerService CreateService(IngestOptions? options = null)
    {
        var opts = Options.Create(options ?? _options);
        return new CircuitBreakerService(opts, _loggerMock.Object);
    }

    #region Initial State Tests

    [Fact]
    public void IsAllowed_NewDomain_ReturnsTrue()
    {
        var service = CreateService();
        
        var result = service.IsAllowed("example.com");
        
        Assert.True(result);
    }

    [Fact]
    public void GetState_NewDomain_ReturnsNull()
    {
        var service = CreateService();
        
        var state = service.GetState("unknown.com");
        
        Assert.Null(state);
    }

    #endregion

    #region Failure Recording Tests

    [Fact]
    public void RecordFailure_BelowThreshold_CircuitRemainsClosed()
    {
        var service = CreateService();
        var domain = "example.com";
        
        // Record failures below threshold (threshold is 3)
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        
        var result = service.IsAllowed(domain);
        
        Assert.True(result);
    }

    [Fact]
    public void RecordFailure_AtThreshold_CircuitOpens()
    {
        var service = CreateService();
        var domain = "example.com";
        
        // Record failures at threshold (threshold is 3)
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        
        var result = service.IsAllowed(domain);
        
        Assert.False(result);
    }

    [Fact]
    public void RecordFailure_AtThreshold_StateIsOpen()
    {
        var service = CreateService();
        var domain = "example.com";
        
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        
        var state = service.GetState(domain);
        
        Assert.NotNull(state);
        Assert.Equal(CircuitState.Open, state.State);
        Assert.NotNull(state.OpenedAt);
    }

    #endregion

    #region URL Normalization Tests

    [Theory]
    [InlineData("https://example.com/path", "example.com")]
    [InlineData("http://EXAMPLE.COM/", "example.com")]
    [InlineData("https://sub.example.com:8080/path?query=1", "sub.example.com")]
    public void RecordFailure_NormalizesDomain(string url, string expectedDomain)
    {
        var service = CreateService();
        
        service.RecordFailure(url);
        
        var state = service.GetState(expectedDomain);
        Assert.NotNull(state);
        Assert.Equal(expectedDomain, state.Domain);
    }

    [Fact]
    public void IsAllowed_AcceptsFullUrl()
    {
        var service = CreateService();
        var url = "https://example.com/some/path";
        
        // Should work with full URL
        var result = service.IsAllowed(url);
        
        Assert.True(result);
    }

    #endregion

    #region Recovery Tests

    [Fact]
    public void IsAllowed_AfterBlockDuration_CircuitCloses()
    {
        // Use short block duration for testing
        var options = new IngestOptions
        {
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureThreshold = 1,
                FailureWindowMinutes = 60,
                BlockDurationMinutes = 0 // Immediate recovery for testing
            }
        };
        var service = CreateService(options);
        var domain = "example.com";
        
        // Trip the circuit
        service.RecordFailure(domain);
        
        // With 0 block duration, should recover immediately
        var result = service.IsAllowed(domain);
        
        Assert.True(result);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsCircuitState()
    {
        var service = CreateService();
        var domain = "example.com";
        
        // Trip the circuit
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        service.RecordFailure(domain);
        
        Assert.False(service.IsAllowed(domain));
        
        // Reset
        service.Reset(domain);
        
        // Should be allowed again and state should be cleared
        Assert.True(service.IsAllowed(domain));
        
        // Note: IsAllowed may recreate a clean state internally,
        // so we check that the circuit is closed rather than null
        var state = service.GetState(domain);
        if (state != null)
        {
            Assert.Equal(CircuitState.Closed, state.State);
            Assert.Equal(0, state.FailureTimestamps.Count);
        }
    }

    #endregion

    #region Success Recording Tests

    [Fact]
    public void RecordSuccess_DoesNotThrow()
    {
        var service = CreateService();
        
        // Should not throw even for unknown domain
        var exception = Record.Exception(() => service.RecordSuccess("example.com"));
        
        Assert.Null(exception);
    }

    #endregion

    #region Multiple Domain Tests

    [Fact]
    public void CircuitBreaker_IsolatesPerDomain()
    {
        var service = CreateService();
        var domain1 = "failing.com";
        var domain2 = "healthy.com";
        
        // Trip circuit for domain1
        service.RecordFailure(domain1);
        service.RecordFailure(domain1);
        service.RecordFailure(domain1);
        
        // domain1 should be blocked
        Assert.False(service.IsAllowed(domain1));
        
        // domain2 should still be allowed
        Assert.True(service.IsAllowed(domain2));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RecordFailure_WithEmptyDomain_HandlesGracefully()
    {
        var service = CreateService();
        
        var exception = Record.Exception(() => service.RecordFailure(""));
        
        Assert.Null(exception);
    }

    [Fact]
    public void IsAllowed_WithEmptyDomain_ReturnsTrue()
    {
        var service = CreateService();
        
        var result = service.IsAllowed("");
        
        Assert.True(result);
    }

    #endregion
}

/// <summary>
/// Tests for FetchResult static factory methods.
/// </summary>
public class FetchResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessResult()
    {
        var result = FetchResult.Succeeded(
            content: "<html>test</html>",
            statusCode: 200,
            contentType: "text/html",
            contentLength: 17,
            finalUrl: "https://example.com/");

        Assert.True(result.Success);
        Assert.Equal("<html>test</html>", result.Content);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("text/html", result.ContentType);
        Assert.Equal(17, result.ContentLength);
        Assert.Equal("https://example.com/", result.FinalUrl);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Failed_CreatesFailureResult()
    {
        var result = FetchResult.Failed("Connection timeout", "TIMEOUT", 0);

        Assert.False(result.Success);
        Assert.Null(result.Content);
        Assert.Equal("Connection timeout", result.Error);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Equal(0, result.StatusCode);
    }

    [Fact]
    public void SsrfBlocked_CreatesBlockedResult()
    {
        var result = FetchResult.SsrfBlocked("http://10.0.0.1/", "Private IP range");

        Assert.False(result.Success);
        Assert.True(result.WasBlockedBySsrf);
        Assert.False(result.WasBlockedByCircuitBreaker);
        Assert.Equal("SSRF_BLOCKED", result.ErrorCode);
        Assert.Contains("SSRF protection", result.Error);
    }

    [Fact]
    public void CircuitBreakerOpen_CreatesBlockedResult()
    {
        var result = FetchResult.CircuitBreakerOpen("failing.com");

        Assert.False(result.Success);
        Assert.True(result.WasBlockedByCircuitBreaker);
        Assert.False(result.WasBlockedBySsrf);
        Assert.Equal("CIRCUIT_BREAKER_OPEN", result.ErrorCode);
        Assert.Contains("failing.com", result.Error);
    }
}
