using Cookbook.Platform.Gateway.Endpoints;
using Cookbook.Platform.Orchestrator.Services.Ingest.Search;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace Cookbook.Platform.Gateway.Tests.Endpoints;

/// <summary>
/// Unit tests for IngestEndpoints.
/// </summary>
public class IngestEndpointsTests
{
    private readonly ISearchProviderResolver _mockResolver;

    public IngestEndpointsTests()
    {
        _mockResolver = Substitute.For<ISearchProviderResolver>();
    }

    #region GET /api/ingest/providers/search Tests

    [Fact]
    public void GetSearchProviders_ReturnsDefaultProviderIdAndProviders()
    {
        // Arrange
        var providers = new List<SearchProviderDescriptor>
        {
            CreateBraveDescriptor(enabled: true, isDefault: true),
            CreateGoogleDescriptor(enabled: true, isDefault: false)
        };
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(providers);

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal("brave", okResult.Value.DefaultProviderId);
        Assert.Equal(2, okResult.Value.Providers.Count);
    }

    [Fact]
    public void GetSearchProviders_WhenNoProvidersEnabled_ReturnsEmptyList()
    {
        // Arrange
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(new List<SearchProviderDescriptor>());

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal("brave", okResult.Value.DefaultProviderId);
        Assert.Empty(okResult.Value.Providers);
    }

    [Fact]
    public void GetSearchProviders_IncludesProviderCapabilities()
    {
        // Arrange
        var providers = new List<SearchProviderDescriptor>
        {
            CreateBraveDescriptor(enabled: true, isDefault: true),
        };
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(providers);

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        var braveProvider = okResult.Value!.Providers.First();
        Assert.True(braveProvider.Capabilities.SupportsMarket);
        Assert.True(braveProvider.Capabilities.SupportsSafeSearch);
        Assert.False(braveProvider.Capabilities.SupportsSiteRestrictions);
        Assert.Equal(20, braveProvider.Capabilities.MaxResultsPerRequest);
        Assert.Equal(15, braveProvider.Capabilities.RateLimitPerMinute);
    }

    [Fact]
    public void GetSearchProviders_GoogleHasSiteRestrictionCapability()
    {
        // Arrange
        var providers = new List<SearchProviderDescriptor>
        {
            CreateGoogleDescriptor(enabled: true, isDefault: false),
        };
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(providers);

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        var googleProvider = okResult.Value!.Providers.First();
        Assert.True(googleProvider.Capabilities.SupportsSiteRestrictions);
        Assert.Equal(10, googleProvider.Capabilities.MaxResultsPerRequest);
    }

    [Fact]
    public void GetSearchProviders_DefaultProviderIsFirst()
    {
        // Arrange
        var providers = new List<SearchProviderDescriptor>
        {
            CreateBraveDescriptor(enabled: true, isDefault: true),
            CreateGoogleDescriptor(enabled: true, isDefault: false)
        };
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(providers);

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        Assert.Equal("brave", okResult.Value!.Providers.First().Id);
        Assert.True(okResult.Value.Providers.First().IsDefault);
    }

    [Fact]
    public void GetSearchProviders_ReturnsOnlyEnabledProviders()
    {
        // Arrange - only enabled providers should be returned by ListEnabled()
        var providers = new List<SearchProviderDescriptor>
        {
            CreateBraveDescriptor(enabled: true, isDefault: true)
        };
        _mockResolver.DefaultProviderId.Returns("brave");
        _mockResolver.ListEnabled().Returns(providers);

        // Act
        var result = InvokeGetSearchProviders();

        // Assert
        var okResult = Assert.IsType<Ok<SearchProvidersResponse>>(result);
        Assert.Single(okResult.Value!.Providers);
        Assert.True(okResult.Value.Providers.All(p => p.Enabled));
    }

    #endregion

    #region Helpers

    private IResult InvokeGetSearchProviders()
    {
        // Use reflection to invoke the private static method
        var method = typeof(IngestEndpoints).GetMethod(
            "GetSearchProviders",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        return (IResult)method!.Invoke(null, [_mockResolver])!;
    }

    private static SearchProviderDescriptor CreateBraveDescriptor(bool enabled, bool isDefault)
    {
        return new SearchProviderDescriptor
        {
            Id = "brave",
            DisplayName = "Brave Search",
            Enabled = enabled,
            IsDefault = isDefault,
            Capabilities = new SearchProviderCapabilities
            {
                SupportsMarket = true,
                SupportsSafeSearch = true,
                SupportsSiteRestrictions = false,
                MaxResultsPerRequest = 20,
                RateLimitPerMinute = 15
            }
        };
    }

    private static SearchProviderDescriptor CreateGoogleDescriptor(bool enabled, bool isDefault)
    {
        return new SearchProviderDescriptor
        {
            Id = "google",
            DisplayName = "Google Custom Search",
            Enabled = enabled,
            IsDefault = isDefault,
            Capabilities = new SearchProviderCapabilities
            {
                SupportsMarket = true,
                SupportsSafeSearch = true,
                SupportsSiteRestrictions = true,
                MaxResultsPerRequest = 10,
                RateLimitPerMinute = 100
            }
        };
    }

    #endregion
}
