using System.Net;
using System.Net.Http.Headers;
using Cookbook.Platform.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// HTTP-based implementation of IFetchService with security protections.
/// </summary>
public class HttpFetchService : IFetchService
{
    private readonly HttpClient _httpClient;
    private readonly ISsrfProtectionService _ssrfProtection;
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly IngestOptions _options;
    private readonly ILogger<HttpFetchService> _logger;

    public HttpFetchService(
        HttpClient httpClient,
        ISsrfProtectionService ssrfProtection,
        ICircuitBreakerService circuitBreaker,
        IOptions<IngestOptions> options,
        ILogger<HttpFetchService> logger)
    {
        _httpClient = httpClient;
        _ssrfProtection = ssrfProtection;
        _circuitBreaker = circuitBreaker;
        _options = options.Value;
        _logger = logger;

        // Configure HttpClient
        _httpClient.Timeout = _options.FetchTimeout;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
    }

    /// <summary>
    /// Fetches content from the specified URL with all security protections.
    /// </summary>
    public async Task<FetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting fetch for URL: {Url}", url);

        // Validate URL format and scheme
        var validation = ValidateUrl(url);
        if (!validation.IsValid)
        {
            _logger.LogWarning("URL validation failed: {Error}", validation.ErrorMessage);
            return FetchResult.Failed(validation.ErrorMessage!, validation.ErrorCode!);
        }

        var uri = new Uri(url);
        var domain = uri.Host;

        // Check circuit breaker
        if (!_circuitBreaker.IsAllowed(domain))
        {
            _logger.LogWarning("Request blocked by circuit breaker for domain: {Domain}", domain);
            return FetchResult.CircuitBreakerOpen(domain);
        }

        // SSRF protection - validate resolved IPs
        var ssrfResult = await _ssrfProtection.ValidateUrlAsync(url, cancellationToken);
        if (!ssrfResult.IsAllowed)
        {
            _logger.LogWarning("Request blocked by SSRF protection: {Reason}", ssrfResult.BlockReason);
            _circuitBreaker.RecordFailure(domain);
            return FetchResult.SsrfBlocked(url, ssrfResult.BlockReason!);
        }

        // Optional robots.txt check
        if (_options.RespectRobotsTxt)
        {
            var robotsAllowed = await CheckRobotsTxtAsync(uri, cancellationToken);
            if (!robotsAllowed)
            {
                _logger.LogInformation("Request blocked by robots.txt for URL: {Url}", url);
                return FetchResult.Failed("Blocked by robots.txt", "ROBOTS_TXT_BLOCKED");
            }
        }

        // Perform fetch with retry
        return await FetchWithRetryAsync(url, domain, cancellationToken);
    }

    /// <summary>
    /// Validates a URL without fetching it.
    /// </summary>
    public (bool IsValid, string? ErrorCode, string? ErrorMessage) ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "EMPTY_URL", "URL cannot be empty");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return (false, "INVALID_URL_FORMAT", "URL is not a valid absolute URI");
        }

        // Validate scheme - only HTTP and HTTPS allowed
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return (false, "INVALID_SCHEME", $"Only HTTP and HTTPS schemes are allowed. Got: {uri.Scheme}");
        }

        // Block URLs with credentials
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return (false, "CREDENTIALS_IN_URL", "URLs with embedded credentials are not allowed");
        }

        // Block local file paths disguised as URLs
        if (uri.IsFile || uri.IsLoopback)
        {
            return (false, "LOCAL_RESOURCE", "Local resources are not allowed");
        }

        return (true, null, null);
    }

    /// <summary>
    /// Performs the fetch with exponential backoff retry.
    /// </summary>
    private async Task<FetchResult> FetchWithRetryAsync(string url, string domain, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _options.MaxFetchRetries;
        Exception? lastException = null;

        while (retryCount <= maxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    // Exponential backoff: 1s, 2s, 4s, etc.
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1));
                    _logger.LogInformation("Retry {Count}/{Max} for {Url} after {Delay}s delay",
                        retryCount, maxRetries, url, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

                var result = await PerformFetchAsync(url, cancellationToken);
                
                if (result.Success)
                {
                    _circuitBreaker.RecordSuccess(domain);
                    return result with { RetryCount = retryCount };
                }

                // Don't retry client errors (4xx)
                if (result.StatusCode >= 400 && result.StatusCode < 500)
                {
                    _circuitBreaker.RecordFailure(domain);
                    return result with { RetryCount = retryCount };
                }

                // Record failure and retry for server errors
                _circuitBreaker.RecordFailure(domain);
                lastException = new HttpRequestException(result.Error);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout
                _logger.LogWarning(ex, "Request timeout for {Url}", url);
                _circuitBreaker.RecordFailure(domain);
                lastException = ex;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching {Url}", url);
                _circuitBreaker.RecordFailure(domain);
                lastException = ex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching {Url}", url);
                _circuitBreaker.RecordFailure(domain);
                lastException = ex;
            }

            retryCount++;
        }

        return FetchResult.Failed(
            $"Failed after {maxRetries} retries: {lastException?.Message}",
            "MAX_RETRIES_EXCEEDED") with { RetryCount = retryCount - 1 };
    }

    /// <summary>
    /// Performs a single fetch attempt.
    /// </summary>
    private async Task<FetchResult> PerformFetchAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var statusCode = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var contentLength = response.Content.Headers.ContentLength ?? 0;
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

        _logger.LogDebug("Received response: {StatusCode}, Content-Type: {ContentType}, Length: {Length}",
            statusCode, contentType, contentLength);

        // Check for successful status code
        if (!response.IsSuccessStatusCode)
        {
            return FetchResult.Failed(
                $"HTTP {statusCode}: {response.ReasonPhrase}",
                $"HTTP_{statusCode}",
                statusCode);
        }

        // Check content length before reading
        if (contentLength > _options.MaxFetchSizeBytes)
        {
            return FetchResult.Failed(
                $"Content too large: {contentLength} bytes exceeds limit of {_options.MaxFetchSizeBytes} bytes",
                "CONTENT_TOO_LARGE",
                statusCode);
        }

        // Read content with size limit
        var content = await ReadContentWithLimitAsync(response.Content, cancellationToken);
        
        if (content == null)
        {
            return FetchResult.Failed(
                $"Content exceeded size limit of {_options.MaxFetchSizeBytes} bytes while reading",
                "CONTENT_TOO_LARGE",
                statusCode);
        }

        _logger.LogInformation("Successfully fetched {Url}: {Length} bytes", url, content.Length);

        return FetchResult.Succeeded(
            content,
            statusCode,
            contentType,
            content.Length,
            finalUrl);
    }

    /// <summary>
    /// Reads content from response with size limit enforcement.
    /// </summary>
    private async Task<string?> ReadContentWithLimitAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        
        var buffer = new char[8192];
        var totalRead = 0L;
        var result = new System.Text.StringBuilder();

        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            totalRead += read;
            if (totalRead > _options.MaxFetchSizeBytes)
            {
                _logger.LogWarning("Content exceeded size limit while reading: {Read} > {Limit}",
                    totalRead, _options.MaxFetchSizeBytes);
                return null;
            }

            result.Append(buffer, 0, read);
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks robots.txt to see if crawling is allowed.
    /// </summary>
    private async Task<bool> CheckRobotsTxtAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var robotsUrl = new Uri(uri, "/robots.txt");
            
            using var request = new HttpRequestMessage(HttpMethod.Get, robotsUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // If robots.txt doesn't exist or can't be fetched, allow the request
                return true;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseRobotsTxt(content, uri.AbsolutePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch robots.txt, allowing request");
            return true;
        }
    }

    /// <summary>
    /// Simple robots.txt parser for our user agent.
    /// </summary>
    private bool ParseRobotsTxt(string content, string path)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var inOurSection = false;
        var disallowedPaths = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            
            if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                var agent = line.Substring(11).Trim();
                inOurSection = agent == "*" || 
                              agent.Contains("CookbookIngestAgent", StringComparison.OrdinalIgnoreCase);
            }
            else if (inOurSection && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                var disallowPath = line.Substring(9).Trim();
                if (!string.IsNullOrEmpty(disallowPath))
                {
                    disallowedPaths.Add(disallowPath);
                }
            }
        }

        // Check if our path matches any disallowed paths
        foreach (var disallowed in disallowedPaths)
        {
            if (disallowed == "/" || path.StartsWith(disallowed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
