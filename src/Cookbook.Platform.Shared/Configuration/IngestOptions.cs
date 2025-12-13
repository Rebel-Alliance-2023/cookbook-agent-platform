namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for the Recipe Ingest Agent.
/// Bind to the "Ingest" section in appsettings.json.
/// </summary>
public class IngestOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Ingest";

    /// <summary>
    /// Maximum number of candidate URLs to return from discovery search.
    /// Default: 10.
    /// </summary>
    public int MaxDiscoveryCandidates { get; set; } = 10;

    /// <summary>
    /// Number of days after which a draft in ReviewReady state expires.
    /// Default: 7 days.
    /// </summary>
    public int DraftExpirationDays { get; set; } = 7;

    /// <summary>
    /// Maximum size in bytes for fetched HTML content.
    /// Default: 5 MB (5,242,880 bytes).
    /// </summary>
    public long MaxFetchSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum character budget for content sent to LLM for extraction.
    /// Default: 60,000 characters.
    /// </summary>
    public int ContentCharacterBudget { get; set; } = 60_000;

    /// <summary>
    /// Whether to respect robots.txt directives when fetching URLs.
    /// Default: true.
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// Maximum size in bytes for individual artifacts.
    /// Default: 1 MB (1,048,576 bytes).
    /// </summary>
    public long MaxArtifactSizeBytes { get; set; } = 1 * 1024 * 1024;

    /// <summary>
    /// User-Agent header to use when fetching URLs.
    /// </summary>
    public string UserAgent { get; set; } = "CookbookIngestAgent/1.0 (+https://github.com/cookbook-agent-platform)";

    /// <summary>
    /// Request timeout in seconds for HTTP fetch operations.
    /// Default: 30 seconds.
    /// </summary>
    public int FetchTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed fetch operations.
    /// Default: 2 retries.
    /// </summary>
    public int MaxFetchRetries { get; set; } = 2;

    /// <summary>
    /// Circuit breaker configuration for per-domain failure handling.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Gets the draft expiration as a TimeSpan.
    /// </summary>
    public TimeSpan DraftExpiration => TimeSpan.FromDays(DraftExpirationDays);

    /// <summary>
    /// Gets the fetch timeout as a TimeSpan.
    /// </summary>
    public TimeSpan FetchTimeout => TimeSpan.FromSeconds(FetchTimeoutSeconds);
}
