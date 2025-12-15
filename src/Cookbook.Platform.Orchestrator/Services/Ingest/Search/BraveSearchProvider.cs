using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest.Search;

/// <summary>
/// Search provider implementation using the Brave Search API.
/// </summary>
public class BraveSearchProvider : ISearchProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly BraveSearchOptions _options;
    private readonly ILogger<BraveSearchProvider> _logger;
    private readonly RateLimiter? _rateLimiter;
    private bool _disposed;

    /// <summary>
    /// The unique provider identifier.
    /// </summary>
    public const string ProviderIdValue = "brave";

    /// <inheritdoc />
    public string ProviderId => ProviderIdValue;

    /// <inheritdoc />
    public string DisplayName => "Brave Search";

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled && _options.IsValid;

    /// <summary>
    /// Initializes a new instance of <see cref="BraveSearchProvider"/>.
    /// </summary>
    public BraveSearchProvider(
        HttpClient httpClient,
        IOptions<BraveSearchOptions> options,
        ILogger<BraveSearchProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure rate limiting if specified
        if (_options.RateLimitPerMinute > 0)
        {
            _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = _options.RateLimitPerMinute,
                TokensPerPeriod = _options.RateLimitPerMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        // Configure HttpClient defaults
        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _options.ApiKey);
        _httpClient.Timeout = _options.Timeout;
    }

    /// <inheritdoc />
    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Brave Search provider is not enabled or not properly configured");
            return SearchResult.Failed("Provider is not enabled", "PROVIDER_DISABLED", ProviderId);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return SearchResult.Failed("Query cannot be empty", "INVALID_QUERY", ProviderId);
        }

        // Check rate limit
        if (_rateLimiter is not null)
        {
            using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                _logger.LogWarning("Rate limit exceeded for Brave Search provider");
                return SearchResult.RateLimited(ProviderId);
            }
        }

        try
        {
            var queryParams = BuildQueryParameters(request);
            var requestUri = $"?{queryParams}";

            _logger.LogDebug("Executing Brave Search: {Query}", request.Query);

            var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                
                if (statusCode == 429)
                {
                    _logger.LogWarning("Brave Search API rate limit hit");
                    return SearchResult.RateLimited(ProviderId);
                }

                if (statusCode == 402)
                {
                    _logger.LogWarning("Brave Search API quota exceeded");
                    return SearchResult.QuotaExceeded(ProviderId);
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Brave Search API error: {StatusCode} - {Error}", statusCode, errorContent);
                return SearchResult.Failed($"API returned status {statusCode}", $"HTTP_{statusCode}", ProviderId);
            }

            var braveResponse = await response.Content.ReadFromJsonAsync<BraveSearchResponse>(
                BraveJsonContext.Default.BraveSearchResponse,
                cancellationToken);

            if (braveResponse is null)
            {
                return SearchResult.Failed("Failed to parse API response", "PARSE_ERROR", ProviderId);
            }

            var candidates = MapToCandidates(braveResponse, request);

            _logger.LogInformation(
                "Brave Search returned {Count} results for query: {Query}",
                candidates.Count,
                request.Query);

            return SearchResult.Succeeded(candidates, ProviderId, braveResponse.Query?.TotalCount);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Brave Search request timed out for query: {Query}", request.Query);
            return SearchResult.Failed("Request timed out", "TIMEOUT", ProviderId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Brave Search for query: {Query}", request.Query);
            return SearchResult.Failed($"HTTP error: {ex.Message}", "HTTP_ERROR", ProviderId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error during Brave Search for query: {Query}", request.Query);
            return SearchResult.Failed($"Parse error: {ex.Message}", "PARSE_ERROR", ProviderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Brave Search for query: {Query}", request.Query);
            return SearchResult.Failed($"Unexpected error: {ex.Message}", "UNKNOWN_ERROR", ProviderId);
        }
    }

    private string BuildQueryParameters(SearchRequest request)
    {
        var parameters = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            $"count={Math.Min(request.MaxResults, _options.MaxResults)}"
        };

        // Market/Country
        var market = request.Market ?? _options.Market;
        if (!string.IsNullOrWhiteSpace(market))
        {
            parameters.Add($"country={Uri.EscapeDataString(market.Split('-').LastOrDefault() ?? "US")}");
        }

        // Safe search
        var safeSearch = request.SafeSearch ?? _options.SafeSearch;
        if (!string.IsNullOrWhiteSpace(safeSearch))
        {
            parameters.Add($"safesearch={Uri.EscapeDataString(safeSearch)}");
        }

        return string.Join("&", parameters);
    }

    private List<SearchCandidate> MapToCandidates(BraveSearchResponse response, SearchRequest request)
    {
        var candidates = new List<SearchCandidate>();
        var webResults = response.Web?.Results ?? [];

        for (var i = 0; i < webResults.Count; i++)
        {
            var result = webResults[i];
            
            if (string.IsNullOrWhiteSpace(result.Url))
                continue;

            // Apply domain filtering
            if (!IsDomainAllowed(result.Url))
            {
                _logger.LogDebug("Filtered out result from denied domain: {Url}", result.Url);
                continue;
            }

            candidates.Add(new SearchCandidate
            {
                Url = result.Url,
                Title = result.Title ?? string.Empty,
                Snippet = result.Description,
                SiteName = ExtractSiteName(result.Url),
                Score = null, // Brave doesn't provide relevance scores
                Position = i
            });
        }

        return candidates;
    }

    private bool IsDomainAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();

        // Check deny list first
        if (_options.DeniedDomains.Count > 0)
        {
            foreach (var denied in _options.DeniedDomains)
            {
                if (host.EndsWith(denied.ToLowerInvariant()))
                    return false;
            }
        }

        // If allow list is specified, check it
        if (_options.AllowedDomains.Count > 0)
        {
            foreach (var allowed in _options.AllowedDomains)
            {
                if (host.EndsWith(allowed.ToLowerInvariant()))
                    return true;
            }
            return false; // Not in allow list
        }

        return true; // No allow list, allow all
    }

    private static string? ExtractSiteName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host;
        
        // Remove www. prefix if present
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];

        return host;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _rateLimiter?.Dispose();
        _disposed = true;
    }
}

#region Brave API Response Models

/// <summary>
/// Root response from Brave Search API.
/// </summary>
internal class BraveSearchResponse
{
    [JsonPropertyName("query")]
    public BraveQuery? Query { get; set; }

    [JsonPropertyName("web")]
    public BraveWebResults? Web { get; set; }
}

/// <summary>
/// Query metadata from Brave response.
/// </summary>
internal class BraveQuery
{
    [JsonPropertyName("original")]
    public string? Original { get; set; }

    [JsonPropertyName("altered")]
    public string? Altered { get; set; }

    [JsonPropertyName("total_count")]
    public long? TotalCount { get; set; }
}

/// <summary>
/// Web results container from Brave response.
/// </summary>
internal class BraveWebResults
{
    [JsonPropertyName("results")]
    public List<BraveWebResult> Results { get; set; } = [];
}

/// <summary>
/// Individual web result from Brave response.
/// </summary>
internal class BraveWebResult
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("page_age")]
    public string? PageAge { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

/// <summary>
/// JSON serialization context for Brave API responses.
/// </summary>
[JsonSerializable(typeof(BraveSearchResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class BraveJsonContext : JsonSerializerContext
{
}

#endregion
