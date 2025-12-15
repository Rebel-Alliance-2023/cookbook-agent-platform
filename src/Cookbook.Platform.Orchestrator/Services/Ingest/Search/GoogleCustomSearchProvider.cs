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
/// Search provider implementation using the Google Custom Search JSON API.
/// </summary>
public class GoogleCustomSearchProvider : ISearchProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GoogleSearchOptions _options;
    private readonly ILogger<GoogleCustomSearchProvider> _logger;
    private readonly RateLimiter? _rateLimiter;
    private bool _disposed;

    /// <summary>
    /// The unique provider identifier.
    /// </summary>
    public const string ProviderIdValue = "google";

    /// <inheritdoc />
    public string ProviderId => ProviderIdValue;

    /// <inheritdoc />
    public string DisplayName => "Google Custom Search";

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled && _options.IsValid;

    /// <summary>
    /// Initializes a new instance of <see cref="GoogleCustomSearchProvider"/>.
    /// </summary>
    public GoogleCustomSearchProvider(
        HttpClient httpClient,
        IOptions<GoogleSearchOptions> options,
        ILogger<GoogleCustomSearchProvider> logger)
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
        _httpClient.Timeout = _options.Timeout;
    }

    /// <inheritdoc />
    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Google Custom Search provider is not enabled or not properly configured");
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
                _logger.LogWarning("Rate limit exceeded for Google Custom Search provider");
                return SearchResult.RateLimited(ProviderId);
            }
        }

        try
        {
            var queryParams = BuildQueryParameters(request);
            var requestUri = $"?{queryParams}";

            _logger.LogDebug("Executing Google Custom Search: {Query}", request.Query);

            var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                
                if (statusCode == 429)
                {
                    _logger.LogWarning("Google Custom Search API rate limit hit");
                    return SearchResult.RateLimited(ProviderId);
                }

                if (statusCode == 403)
                {
                    // Google returns 403 for quota exceeded
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (errorContent.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                        errorContent.Contains("limit", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Google Custom Search API quota exceeded");
                        return SearchResult.QuotaExceeded(ProviderId);
                    }
                    
                    _logger.LogError("Google Custom Search API forbidden: {Error}", errorContent);
                    return SearchResult.Failed($"API access forbidden", "HTTP_403", ProviderId);
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Google Custom Search API error: {StatusCode} - {Error}", statusCode, errorBody);
                return SearchResult.Failed($"API returned status {statusCode}", $"HTTP_{statusCode}", ProviderId);
            }

            var googleResponse = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>(
                GoogleJsonContext.Default.GoogleSearchResponse,
                cancellationToken);

            if (googleResponse is null)
            {
                return SearchResult.Failed("Failed to parse API response", "PARSE_ERROR", ProviderId);
            }

            var candidates = MapToCandidates(googleResponse, request);

            // Parse total results from searchInformation
            long? totalResults = null;
            if (long.TryParse(googleResponse.SearchInformation?.TotalResults, out var parsedTotal))
            {
                totalResults = parsedTotal;
            }

            _logger.LogInformation(
                "Google Custom Search returned {Count} results for query: {Query}",
                candidates.Count,
                request.Query);

            return SearchResult.Succeeded(candidates, ProviderId, totalResults);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Google Custom Search request timed out for query: {Query}", request.Query);
            return SearchResult.Failed("Request timed out", "TIMEOUT", ProviderId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during Google Custom Search for query: {Query}", request.Query);
            return SearchResult.Failed($"HTTP error: {ex.Message}", "HTTP_ERROR", ProviderId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error during Google Custom Search for query: {Query}", request.Query);
            return SearchResult.Failed($"Parse error: {ex.Message}", "PARSE_ERROR", ProviderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Google Custom Search for query: {Query}", request.Query);
            return SearchResult.Failed($"Unexpected error: {ex.Message}", "UNKNOWN_ERROR", ProviderId);
        }
    }

    private string BuildQueryParameters(SearchRequest request)
    {
        var parameters = new List<string>
        {
            $"key={Uri.EscapeDataString(_options.ApiKey)}",
            $"cx={Uri.EscapeDataString(_options.SearchEngineId)}",
            $"q={Uri.EscapeDataString(BuildSearchQuery(request))}",
            $"num={Math.Min(request.MaxResults, _options.MaxResults)}"
        };

        // Language
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            parameters.Add($"lr=lang_{_options.Language}");
        }

        // Country/Region
        if (!string.IsNullOrWhiteSpace(_options.Country))
        {
            parameters.Add($"gl={_options.Country}");
        }

        // Safe search
        var safeSearch = request.SafeSearch ?? _options.SafeSearch;
        if (!string.IsNullOrWhiteSpace(safeSearch))
        {
            // Google uses: off, medium, high (we normalize moderate -> medium)
            var googleSafe = safeSearch.Equals("moderate", StringComparison.OrdinalIgnoreCase) 
                ? "medium" 
                : safeSearch;
            parameters.Add($"safe={Uri.EscapeDataString(googleSafe)}");
        }

        return string.Join("&", parameters);
    }

    private string BuildSearchQuery(SearchRequest request)
    {
        var query = request.Query;

        // Apply site restrictions if enabled
        if (_options.UseSiteRestrictions && _options.SiteRestrictions.Count > 0)
        {
            var siteQuery = string.Join(" OR ", _options.SiteRestrictions.Select(s => $"site:{s}"));
            query = $"({siteQuery}) {query}";
        }

        return query;
    }

    private List<SearchCandidate> MapToCandidates(GoogleSearchResponse response, SearchRequest request)
    {
        var candidates = new List<SearchCandidate>();
        var items = response.Items ?? [];

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            
            if (string.IsNullOrWhiteSpace(item.Link))
                continue;

            // Apply domain filtering
            if (!IsDomainAllowed(item.Link))
            {
                _logger.LogDebug("Filtered out result from denied domain: {Url}", item.Link);
                continue;
            }

            candidates.Add(new SearchCandidate
            {
                Url = item.Link,
                Title = item.Title ?? string.Empty,
                Snippet = item.Snippet,
                SiteName = item.DisplayLink ?? ExtractSiteName(item.Link),
                Score = null, // Google doesn't provide relevance scores in CSE
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

#region Google API Response Models

/// <summary>
/// Root response from Google Custom Search API.
/// </summary>
internal class GoogleSearchResponse
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("searchInformation")]
    public GoogleSearchInformation? SearchInformation { get; set; }

    [JsonPropertyName("items")]
    public List<GoogleSearchItem>? Items { get; set; }
}

/// <summary>
/// Search information metadata from Google response.
/// </summary>
internal class GoogleSearchInformation
{
    [JsonPropertyName("searchTime")]
    public double SearchTime { get; set; }

    [JsonPropertyName("formattedSearchTime")]
    public string? FormattedSearchTime { get; set; }

    [JsonPropertyName("totalResults")]
    public string? TotalResults { get; set; }

    [JsonPropertyName("formattedTotalResults")]
    public string? FormattedTotalResults { get; set; }
}

/// <summary>
/// Individual search result item from Google response.
/// </summary>
internal class GoogleSearchItem
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("htmlTitle")]
    public string? HtmlTitle { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("displayLink")]
    public string? DisplayLink { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("htmlSnippet")]
    public string? HtmlSnippet { get; set; }

    [JsonPropertyName("formattedUrl")]
    public string? FormattedUrl { get; set; }

    [JsonPropertyName("htmlFormattedUrl")]
    public string? HtmlFormattedUrl { get; set; }
}

/// <summary>
/// JSON serialization context for Google API responses.
/// </summary>
[JsonSerializable(typeof(GoogleSearchResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class GoogleJsonContext : JsonSerializerContext
{
}

#endregion
