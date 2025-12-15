using Cookbook.Platform.Shared.Models.Ingest.Search;

namespace Cookbook.Platform.Orchestrator.Services.Ingest.Search;

/// <summary>
/// Result of a search operation from a search provider.
/// </summary>
public record SearchResult
{
    /// <summary>
    /// Whether the search operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The list of search candidates returned by the provider.
    /// </summary>
    public IReadOnlyList<SearchCandidate> Candidates { get; init; } = [];

    /// <summary>
    /// The total number of results available (may be larger than returned candidates).
    /// </summary>
    public long? TotalResults { get; init; }

    /// <summary>
    /// Error message if the search failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Error code for structured error handling.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The provider ID that executed this search.
    /// </summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// The timestamp when the search was executed.
    /// </summary>
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful search result.
    /// </summary>
    public static SearchResult Succeeded(IReadOnlyList<SearchCandidate> candidates, string providerId, long? totalResults = null) => new()
    {
        Success = true,
        Candidates = candidates,
        ProviderId = providerId,
        TotalResults = totalResults ?? candidates.Count
    };

    /// <summary>
    /// Creates a failed search result.
    /// </summary>
    public static SearchResult Failed(string error, string errorCode, string? providerId = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode,
        ProviderId = providerId
    };

    /// <summary>
    /// Creates a rate-limited result.
    /// </summary>
    public static SearchResult RateLimited(string providerId) => new()
    {
        Success = false,
        Error = $"Rate limit exceeded for provider: {providerId}",
        ErrorCode = "RATE_LIMITED",
        ProviderId = providerId
    };

    /// <summary>
    /// Creates a quota-exceeded result.
    /// </summary>
    public static SearchResult QuotaExceeded(string providerId) => new()
    {
        Success = false,
        Error = $"Quota exceeded for provider: {providerId}",
        ErrorCode = "QUOTA_EXCEEDED",
        ProviderId = providerId
    };
}

/// <summary>
/// Interface for search providers that discover recipe URLs from search queries.
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// Gets the unique identifier for this search provider.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the display name for this search provider.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether this provider is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Searches for recipe URLs based on the specified request.
    /// </summary>
    /// <param name="request">The search request containing query and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The search result containing candidates or error information.</returns>
    Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
