namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for search providers.
/// Bind to the "Ingest:Search" section in appsettings.json.
/// </summary>
public class SearchOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Ingest:Search";

    /// <summary>
    /// The ID of the default search provider to use when none is specified.
    /// Default: "brave".
    /// </summary>
    public string DefaultProvider { get; set; } = "brave";

    /// <summary>
    /// Whether to allow fallback to the default provider when the selected provider fails.
    /// Default: false.
    /// </summary>
    public bool AllowFallback { get; set; } = false;
}
