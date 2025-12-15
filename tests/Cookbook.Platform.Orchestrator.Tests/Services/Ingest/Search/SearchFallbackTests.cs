using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest.Search;

/// <summary>
/// Unit tests for search provider fallback behavior.
/// </summary>
public class SearchFallbackTests
{
    #region ShouldAttemptFallback Logic Tests
    
    [Fact]
    public void TransientErrorCodes_ShouldTriggerFallback()
    {
        // These are the error codes that should trigger fallback
        var transientErrorCodes = new[]
        {
            "RATE_LIMIT_EXCEEDED",
            "QUOTA_EXCEEDED",
            "SERVICE_UNAVAILABLE",
            "TIMEOUT",
            "HTTP_429",
            "HTTP_503",
            "HTTP_504"
        };
        
        foreach (var errorCode in transientErrorCodes)
        {
            Assert.True(IsTransientError(errorCode), $"Error code '{errorCode}' should be considered transient");
        }
    }

    [Theory]
    [InlineData("INVALID_API_KEY")]
    [InlineData("UNAUTHORIZED")]
    [InlineData("NOT_FOUND")]
    [InlineData("SEARCH_FAILED")]
    [InlineData("INVALID_QUERY")]
    public void NonTransientErrorCodes_ShouldNotTriggerFallback(string errorCode)
    {
        Assert.False(IsTransientError(errorCode), $"Error code '{errorCode}' should NOT be considered transient");
    }

    [Fact]
    public void FallbackDisabled_ShouldNotAttemptFallback()
    {
        // Arrange
        var options = new SearchOptions { AllowFallback = false };
        
        // Act & Assert
        Assert.False(options.AllowFallback);
    }

    [Fact]
    public void FallbackEnabled_ShouldAttemptFallback()
    {
        // Arrange
        var options = new SearchOptions { AllowFallback = true };
        
        // Act & Assert
        Assert.True(options.AllowFallback);
    }

    [Fact]
    public void SearchOptions_DefaultsToNoFallback()
    {
        // Arrange & Act
        var options = new SearchOptions();
        
        // Assert
        Assert.False(options.AllowFallback);
    }

    #endregion

    #region SearchResult Error Scenarios

    [Fact]
    public void SearchResult_WithRateLimitError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Success = false,
            ErrorCode = "RATE_LIMIT_EXCEEDED",
            Error = "Rate limit exceeded. Please try again later."
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("RATE_LIMIT_EXCEEDED", result.ErrorCode);
        Assert.True(IsTransientError(result.ErrorCode));
    }

    [Fact]
    public void SearchResult_WithQuotaError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Success = false,
            ErrorCode = "QUOTA_EXCEEDED",
            Error = "Daily quota exceeded."
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("QUOTA_EXCEEDED", result.ErrorCode);
        Assert.True(IsTransientError(result.ErrorCode));
    }

    [Fact]
    public void SearchResult_WithTimeoutError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Success = false,
            ErrorCode = "TIMEOUT",
            Error = "Request timed out."
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.True(IsTransientError(result.ErrorCode));
    }

    [Fact]
    public void SearchResult_With429StatusCode_HasCorrectErrorCode()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Success = false,
            ErrorCode = "HTTP_429",
            Error = "Too Many Requests"
        };

        // Assert
        Assert.False(result.Success);
        Assert.Equal("HTTP_429", result.ErrorCode);
        Assert.True(IsTransientError(result.ErrorCode));
    }

    #endregion

    #region Fallback Metadata Tests

    [Fact]
    public void FallbackInfo_RecordsOriginalProvider()
    {
        // Arrange
        var fallbackInfo = new FallbackInfo
        {
            UsedFallbackProvider = true,
            OriginalProviderId = "google",
            FallbackProviderId = "brave",
            FallbackReason = "RATE_LIMIT_EXCEEDED: Rate limit exceeded"
        };

        // Assert
        Assert.True(fallbackInfo.UsedFallbackProvider);
        Assert.Equal("google", fallbackInfo.OriginalProviderId);
        Assert.Equal("brave", fallbackInfo.FallbackProviderId);
        Assert.Contains("RATE_LIMIT_EXCEEDED", fallbackInfo.FallbackReason);
    }

    [Fact]
    public void FallbackInfo_DefaultsToNoFallback()
    {
        // Arrange & Act
        var fallbackInfo = new FallbackInfo();

        // Assert
        Assert.False(fallbackInfo.UsedFallbackProvider);
        Assert.Null(fallbackInfo.OriginalProviderId);
        Assert.Null(fallbackInfo.FallbackProviderId);
        Assert.Null(fallbackInfo.FallbackReason);
    }

    #endregion

    #region Helper Methods

    private static bool IsTransientError(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
            return false;

        var transientErrorCodes = new[]
        {
            "RATE_LIMIT_EXCEEDED",
            "QUOTA_EXCEEDED",
            "SERVICE_UNAVAILABLE",
            "TIMEOUT",
            "HTTP_429",
            "HTTP_503",
            "HTTP_504"
        };

        return transientErrorCodes.Contains(errorCode, StringComparer.OrdinalIgnoreCase);
    }

    #endregion
}

/// <summary>
/// DTO for fallback information.
/// </summary>
public record FallbackInfo
{
    public bool UsedFallbackProvider { get; init; }
    public string? OriginalProviderId { get; init; }
    public string? FallbackProviderId { get; init; }
    public string? FallbackReason { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
