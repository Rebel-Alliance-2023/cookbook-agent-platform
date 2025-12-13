namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Result of a URL fetch operation.
/// </summary>
public record FetchResult
{
    /// <summary>
    /// Whether the fetch operation was successful.
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// The fetched HTML content (if successful).
    /// </summary>
    public string? Content { get; init; }
    
    /// <summary>
    /// The HTTP status code returned by the server.
    /// </summary>
    public int StatusCode { get; init; }
    
    /// <summary>
    /// The Content-Type header value from the response.
    /// </summary>
    public string? ContentType { get; init; }
    
    /// <summary>
    /// The content length in bytes.
    /// </summary>
    public long ContentLength { get; init; }
    
    /// <summary>
    /// The final URL after any redirects.
    /// </summary>
    public string? FinalUrl { get; init; }
    
    /// <summary>
    /// Error message if the fetch failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Error code for structured error handling.
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// The timestamp when the content was retrieved.
    /// </summary>
    public DateTime RetrievedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether the failure was due to SSRF protection.
    /// </summary>
    public bool WasBlockedBySsrf { get; init; }
    
    /// <summary>
    /// Whether the failure was due to circuit breaker.
    /// </summary>
    public bool WasBlockedByCircuitBreaker { get; init; }
    
    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Creates a successful fetch result.
    /// </summary>
    public static FetchResult Succeeded(string content, int statusCode, string? contentType, long contentLength, string finalUrl) => new()
    {
        Success = true,
        Content = content,
        StatusCode = statusCode,
        ContentType = contentType,
        ContentLength = contentLength,
        FinalUrl = finalUrl
    };

    /// <summary>
    /// Creates a failed fetch result.
    /// </summary>
    public static FetchResult Failed(string error, string errorCode, int statusCode = 0) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode,
        StatusCode = statusCode
    };

    /// <summary>
    /// Creates a result for SSRF-blocked request.
    /// </summary>
    public static FetchResult SsrfBlocked(string url, string reason) => new()
    {
        Success = false,
        Error = $"Request blocked by SSRF protection: {reason}",
        ErrorCode = "SSRF_BLOCKED",
        WasBlockedBySsrf = true
    };

    /// <summary>
    /// Creates a result for circuit breaker-blocked request.
    /// </summary>
    public static FetchResult CircuitBreakerOpen(string domain) => new()
    {
        Success = false,
        Error = $"Circuit breaker is open for domain: {domain}",
        ErrorCode = "CIRCUIT_BREAKER_OPEN",
        WasBlockedByCircuitBreaker = true
    };
}

/// <summary>
/// Service for fetching content from URLs with security protections.
/// </summary>
public interface IFetchService
{
    /// <summary>
    /// Fetches content from the specified URL.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fetch result containing content or error information.</returns>
    Task<FetchResult> FetchAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a URL without fetching it.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is valid and allowed, false otherwise.</returns>
    (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateUrl(string url);
}
