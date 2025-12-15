using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest.Search;

/// <summary>
/// Resolves search providers from DI-registered implementations.
/// </summary>
public class SearchProviderResolver : ISearchProviderResolver
{
    private readonly IEnumerable<ISearchProvider> _providers;
    private readonly SearchOptions _options;
    private readonly ILogger<SearchProviderResolver> _logger;
    private readonly Dictionary<string, ISearchProvider> _providerMap;
    private readonly Dictionary<string, SearchProviderDescriptor> _descriptorMap;

    /// <inheritdoc />
    public string DefaultProviderId => _options.DefaultProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="SearchProviderResolver"/>.
    /// </summary>
    public SearchProviderResolver(
        IEnumerable<ISearchProvider> providers,
        IOptions<SearchOptions> options,
        IOptions<BraveSearchOptions> braveOptions,
        IOptions<GoogleSearchOptions> googleOptions,
        ILogger<SearchProviderResolver> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Build provider lookup map
        _providerMap = _providers.ToDictionary(p => p.ProviderId, p => p, StringComparer.OrdinalIgnoreCase);

        // Build descriptor map with capabilities
        _descriptorMap = BuildDescriptorMap(braveOptions.Value, googleOptions.Value);

        _logger.LogInformation(
            "SearchProviderResolver initialized with {Count} providers. Default: {DefaultProvider}",
            _providerMap.Count,
            DefaultProviderId);
    }

    private Dictionary<string, SearchProviderDescriptor> BuildDescriptorMap(
        BraveSearchOptions braveOptions,
        GoogleSearchOptions googleOptions)
    {
        var descriptors = new Dictionary<string, SearchProviderDescriptor>(StringComparer.OrdinalIgnoreCase);

        // Brave descriptor
        if (_providerMap.TryGetValue(BraveSearchProvider.ProviderIdValue, out var braveProvider))
        {
            descriptors[BraveSearchProvider.ProviderIdValue] = new SearchProviderDescriptor
            {
                Id = BraveSearchProvider.ProviderIdValue,
                DisplayName = braveProvider.DisplayName,
                Enabled = braveProvider.IsEnabled,
                IsDefault = braveOptions.IsDefault || 
                           DefaultProviderId.Equals(BraveSearchProvider.ProviderIdValue, StringComparison.OrdinalIgnoreCase),
                Capabilities = new SearchProviderCapabilities
                {
                    SupportsMarket = true,
                    SupportsSafeSearch = true,
                    SupportsSiteRestrictions = false,
                    MaxResultsPerRequest = Math.Min(braveOptions.MaxResults, 20),
                    RateLimitPerMinute = braveOptions.RateLimitPerMinute
                }
            };
        }

        // Google descriptor
        if (_providerMap.TryGetValue(GoogleCustomSearchProvider.ProviderIdValue, out var googleProvider))
        {
            descriptors[GoogleCustomSearchProvider.ProviderIdValue] = new SearchProviderDescriptor
            {
                Id = GoogleCustomSearchProvider.ProviderIdValue,
                DisplayName = googleProvider.DisplayName,
                Enabled = googleProvider.IsEnabled,
                IsDefault = googleOptions.IsDefault ||
                           DefaultProviderId.Equals(GoogleCustomSearchProvider.ProviderIdValue, StringComparison.OrdinalIgnoreCase),
                Capabilities = new SearchProviderCapabilities
                {
                    SupportsMarket = true,
                    SupportsSafeSearch = true,
                    SupportsSiteRestrictions = true, // Google supports site: operator
                    MaxResultsPerRequest = Math.Min(googleOptions.MaxResults, 10),
                    RateLimitPerMinute = googleOptions.RateLimitPerMinute
                }
            };
        }

        return descriptors;
    }

    /// <inheritdoc />
    public ISearchProvider Resolve(string? providerId)
    {
        var effectiveId = string.IsNullOrWhiteSpace(providerId) ? DefaultProviderId : providerId;

        if (!_providerMap.TryGetValue(effectiveId, out var provider))
        {
            _logger.LogWarning("Search provider '{ProviderId}' not found", effectiveId);
            throw SearchProviderNotFoundException.Unknown(effectiveId);
        }

        if (!provider.IsEnabled)
        {
            _logger.LogWarning("Search provider '{ProviderId}' is disabled", effectiveId);
            throw SearchProviderNotFoundException.Disabled(effectiveId);
        }

        _logger.LogDebug("Resolved search provider: {ProviderId}", effectiveId);
        return provider;
    }

    /// <inheritdoc />
    public bool TryResolve(string? providerId, out ISearchProvider? provider)
    {
        try
        {
            provider = Resolve(providerId);
            return true;
        }
        catch (SearchProviderNotFoundException)
        {
            provider = null;
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SearchProviderDescriptor> ListEnabled()
    {
        return _descriptorMap.Values
            .Where(d => d.Enabled)
            .OrderByDescending(d => d.IsDefault) // Default first
            .ThenBy(d => d.DisplayName)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<SearchProviderDescriptor> ListAll()
    {
        return _descriptorMap.Values
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.DisplayName)
            .ToList();
    }

    /// <inheritdoc />
    public SearchProviderDescriptor? GetDescriptor(string providerId)
    {
        return _descriptorMap.TryGetValue(providerId, out var descriptor) ? descriptor : null;
    }
}
