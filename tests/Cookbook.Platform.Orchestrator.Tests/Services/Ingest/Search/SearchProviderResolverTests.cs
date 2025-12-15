using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest.Search;

/// <summary>
/// Unit tests for SearchProviderResolver.
/// </summary>
public class SearchProviderResolverTests
{
    private readonly Mock<ILogger<SearchProviderResolver>> _loggerMock;
    private readonly SearchOptions _searchOptions;
    private readonly BraveSearchOptions _braveOptions;
    private readonly GoogleSearchOptions _googleOptions;

    public SearchProviderResolverTests()
    {
        _loggerMock = new Mock<ILogger<SearchProviderResolver>>();
        _searchOptions = new SearchOptions { DefaultProvider = "brave" };
        _braveOptions = new BraveSearchOptions
        {
            ApiKey = "test-key",
            Enabled = true,
            IsDefault = true,
            MaxResults = 10,
            RateLimitPerMinute = 15
        };
        _googleOptions = new GoogleSearchOptions
        {
            ApiKey = "test-key",
            SearchEngineId = "test-cx",
            Enabled = true,
            IsDefault = false,
            MaxResults = 10,
            RateLimitPerMinute = 100
        };
    }

    private SearchProviderResolver CreateResolver(IEnumerable<ISearchProvider> providers)
    {
        return new SearchProviderResolver(
            providers,
            Options.Create(_searchOptions),
            Options.Create(_braveOptions),
            Options.Create(_googleOptions),
            _loggerMock.Object);
    }

    private static Mock<ISearchProvider> CreateMockProvider(string id, string displayName, bool isEnabled)
    {
        var mock = new Mock<ISearchProvider>();
        mock.Setup(p => p.ProviderId).Returns(id);
        mock.Setup(p => p.DisplayName).Returns(displayName);
        mock.Setup(p => p.IsEnabled).Returns(isEnabled);
        return mock;
    }

    #region DefaultProviderId Tests

    [Fact]
    public void DefaultProviderId_ReturnsConfiguredDefault()
    {
        _searchOptions.DefaultProvider = "google";
        var providers = new[] { CreateMockProvider("google", "Google", true).Object };
        var resolver = CreateResolver(providers);

        Assert.Equal("google", resolver.DefaultProviderId);
    }

    #endregion

    #region Resolve Tests

    [Fact]
    public void Resolve_WhenProviderExists_ReturnsProvider()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.Resolve("brave");

        Assert.Same(braveProvider.Object, result);
    }

    [Fact]
    public void Resolve_WhenProviderIdIsNull_ReturnsDefaultProvider()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.Resolve(null);

        Assert.Same(braveProvider.Object, result);
    }

    [Fact]
    public void Resolve_WhenProviderIdIsEmpty_ReturnsDefaultProvider()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.Resolve("");

        Assert.Same(braveProvider.Object, result);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.Resolve("BRAVE");

        Assert.Same(braveProvider.Object, result);
    }

    [Fact]
    public void Resolve_WhenProviderNotFound_ThrowsSearchProviderNotFoundException()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var ex = Assert.Throws<SearchProviderNotFoundException>(() => resolver.Resolve("unknown"));

        Assert.Equal("unknown", ex.ProviderId);
        Assert.Equal("UNKNOWN_SEARCH_PROVIDER", ex.ErrorCode);
    }

    [Fact]
    public void Resolve_WhenProviderIsDisabled_ThrowsSearchProviderNotFoundException()
    {
        var disabledProvider = CreateMockProvider("brave", "Brave Search", false);
        var resolver = CreateResolver(new[] { disabledProvider.Object });

        var ex = Assert.Throws<SearchProviderNotFoundException>(() => resolver.Resolve("brave"));

        Assert.Equal("brave", ex.ProviderId);
        Assert.Equal("DISABLED_SEARCH_PROVIDER", ex.ErrorCode);
    }

    #endregion

    #region TryResolve Tests

    [Fact]
    public void TryResolve_WhenProviderExists_ReturnsTrueAndProvider()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.TryResolve("brave", out var provider);

        Assert.True(result);
        Assert.Same(braveProvider.Object, provider);
    }

    [Fact]
    public void TryResolve_WhenProviderNotFound_ReturnsFalseAndNull()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var result = resolver.TryResolve("unknown", out var provider);

        Assert.False(result);
        Assert.Null(provider);
    }

    [Fact]
    public void TryResolve_WhenProviderDisabled_ReturnsFalseAndNull()
    {
        var disabledProvider = CreateMockProvider("brave", "Brave Search", false);
        var resolver = CreateResolver(new[] { disabledProvider.Object });

        var result = resolver.TryResolve("brave", out var provider);

        Assert.False(result);
        Assert.Null(provider);
    }

    #endregion

    #region ListEnabled Tests

    [Fact]
    public void ListEnabled_ReturnsOnlyEnabledProviders()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var googleProvider = CreateMockProvider("google", "Google", true);
        _googleOptions.Enabled = true;
        
        var resolver = CreateResolver(new[] { braveProvider.Object, googleProvider.Object });

        var enabled = resolver.ListEnabled();

        Assert.Equal(2, enabled.Count);
    }

    [Fact]
    public void ListEnabled_ExcludesDisabledProviders()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var googleProvider = CreateMockProvider("google", "Google", false);
        
        var resolver = CreateResolver(new[] { braveProvider.Object, googleProvider.Object });

        var enabled = resolver.ListEnabled();

        Assert.Single(enabled);
        Assert.Equal("brave", enabled[0].Id);
    }

    [Fact]
    public void ListEnabled_DefaultProviderIsFirst()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var googleProvider = CreateMockProvider("google", "Google Custom Search", true);
        
        var resolver = CreateResolver(new[] { googleProvider.Object, braveProvider.Object });

        var enabled = resolver.ListEnabled();

        Assert.Equal("brave", enabled[0].Id); // Default should be first
    }

    #endregion

    #region ListAll Tests

    [Fact]
    public void ListAll_ReturnsAllProviders()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var googleProvider = CreateMockProvider("google", "Google", false);
        
        var resolver = CreateResolver(new[] { braveProvider.Object, googleProvider.Object });

        var all = resolver.ListAll();

        Assert.Equal(2, all.Count);
    }

    #endregion

    #region GetDescriptor Tests

    [Fact]
    public void GetDescriptor_WhenProviderExists_ReturnsDescriptor()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var descriptor = resolver.GetDescriptor("brave");

        Assert.NotNull(descriptor);
        Assert.Equal("brave", descriptor.Id);
        Assert.Equal("Brave Search", descriptor.DisplayName);
        Assert.True(descriptor.Enabled);
        Assert.True(descriptor.IsDefault);
    }

    [Fact]
    public void GetDescriptor_WhenProviderNotFound_ReturnsNull()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var descriptor = resolver.GetDescriptor("unknown");

        Assert.Null(descriptor);
    }

    [Fact]
    public void GetDescriptor_IncludesCapabilities()
    {
        var braveProvider = CreateMockProvider("brave", "Brave Search", true);
        var resolver = CreateResolver(new[] { braveProvider.Object });

        var descriptor = resolver.GetDescriptor("brave");

        Assert.NotNull(descriptor);
        Assert.True(descriptor.Capabilities.SupportsMarket);
        Assert.True(descriptor.Capabilities.SupportsSafeSearch);
        Assert.False(descriptor.Capabilities.SupportsSiteRestrictions);
        Assert.Equal(10, descriptor.Capabilities.MaxResultsPerRequest);
        Assert.Equal(15, descriptor.Capabilities.RateLimitPerMinute);
    }

    [Fact]
    public void GetDescriptor_GoogleHasSiteRestrictionCapability()
    {
        var googleProvider = CreateMockProvider("google", "Google Custom Search", true);
        var resolver = CreateResolver(new[] { googleProvider.Object });

        var descriptor = resolver.GetDescriptor("google");

        Assert.NotNull(descriptor);
        Assert.True(descriptor.Capabilities.SupportsSiteRestrictions);
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void SearchProviderNotFoundException_Unknown_HasCorrectProperties()
    {
        var ex = SearchProviderNotFoundException.Unknown("test-provider");

        Assert.Equal("test-provider", ex.ProviderId);
        Assert.Equal("UNKNOWN_SEARCH_PROVIDER", ex.ErrorCode);
        Assert.Contains("test-provider", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void SearchProviderNotFoundException_Disabled_HasCorrectProperties()
    {
        var ex = SearchProviderNotFoundException.Disabled("test-provider");

        Assert.Equal("test-provider", ex.ProviderId);
        Assert.Equal("DISABLED_SEARCH_PROVIDER", ex.ErrorCode);
        Assert.Contains("test-provider", ex.Message);
        Assert.Contains("disabled", ex.Message);
    }

    #endregion
}
