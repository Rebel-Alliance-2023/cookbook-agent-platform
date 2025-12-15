namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for the Google Custom Search API provider.
/// Bind to the "Ingest:Search:Google" section in appsettings.json.
/// </summary>
public class GoogleSearchOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Ingest:Search:Google";

    /// <summary>
    /// The Google Custom Search API key.
    /// Should be stored in user secrets for development.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Custom Search Engine ID (cx parameter).
    /// Created at https://programmablesearchengine.google.com/
    /// </summary>
    public string SearchEngineId { get; set; } = string.Empty;

    /// <summary>
    /// The Google Custom Search API endpoint.
    /// Default: https://www.googleapis.com/customsearch/v1
    /// </summary>
    public string Endpoint { get; set; } = "https://www.googleapis.com/customsearch/v1";

    /// <summary>
    /// The language for search results (e.g., "en", "fr", "de").
    /// Uses Google's language codes.
    /// Default: "en".
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// The country/region for search results (e.g., "us", "gb", "de").
    /// Uses ISO 3166-1 alpha-2 country codes.
    /// Default: "us".
    /// </summary>
    public string Country { get; set; } = "us";

    /// <summary>
    /// Maximum number of results to return per search.
    /// Default: 10. Maximum allowed by API: 10 per request.
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
    /// Default: 100 (Google CSE free tier is 100/day, so be conservative).
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Whether to use site-restricted mode.
    /// When true, restricts search to domains in SiteRestrictions list.
    /// </summary>
    public bool UseSiteRestrictions { get; set; } = false;

    /// <summary>
    /// List of domains to restrict search to (site-restricted mode).
    /// Only used when UseSiteRestrictions is true.
    /// </summary>
    public List<string> SiteRestrictions { get; set; } = [];

    /// <summary>
    /// List of domains to allow (whitelist). If empty, all domains are allowed.
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// List of domains to deny (blacklist). Applied after allow list.
    /// </summary>
    public List<string> DeniedDomains { get; set; } = [];

    /// <summary>
    /// Safe search filtering level: "off", "medium", or "high".
    /// Default: "medium".
    /// </summary>
    public string SafeSearch { get; set; } = "medium";

    /// <summary>
    /// Gets the timeout as a TimeSpan.
    /// </summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    /// <summary>
    /// Validates that required configuration is present.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ApiKey) 
                           && !string.IsNullOrWhiteSpace(SearchEngineId)
                           && !string.IsNullOrWhiteSpace(Endpoint);
}
