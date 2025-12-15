namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for the Brave Search API provider.
/// Bind to the "Ingest:Search:Brave" section in appsettings.json.
/// </summary>
public class BraveSearchOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Ingest:Search:Brave";

    /// <summary>
    /// The Brave Search API key.
    /// Should be stored in user secrets for development.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Brave Search API endpoint.
    /// Default: https://api.search.brave.com/res/v1/web/search
    /// </summary>
    public string Endpoint { get; set; } = "https://api.search.brave.com/res/v1/web/search";

    /// <summary>
    /// The market/region for search results (e.g., "en-US", "en-GB").
    /// Default: "en-US".
    /// </summary>
    public string Market { get; set; } = "en-US";

    /// <summary>
    /// Safe search filtering level: "off", "moderate", or "strict".
    /// Default: "moderate".
    /// </summary>
    public string SafeSearch { get; set; } = "moderate";

    /// <summary>
    /// Maximum number of results to return per search.
    /// Default: 10. Maximum allowed by API: 20.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Whether this provider is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this is the default search provider.
    /// Default: false.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Request timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Rate limit: maximum requests per minute.
    /// Default: 15 (Brave free tier limit).
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 15;

    /// <summary>
    /// List of domains to allow (whitelist). If empty, all domains are allowed.
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// List of domains to deny (blacklist). Applied after allow list.
    /// </summary>
    public List<string> DeniedDomains { get; set; } = [];

    /// <summary>
    /// Gets the timeout as a TimeSpan.
    /// </summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    /// <summary>
    /// Validates that required configuration is present.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Endpoint);
}
