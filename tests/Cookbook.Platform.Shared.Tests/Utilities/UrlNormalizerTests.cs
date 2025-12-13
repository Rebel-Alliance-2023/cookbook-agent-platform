using Cookbook.Platform.Shared.Utilities;

namespace Cookbook.Platform.Shared.Tests.Utilities;

/// <summary>
/// Unit tests for the UrlNormalizer class.
/// </summary>
public class UrlNormalizerTests
{
    #region Basic Normalization

    [Fact]
    public void Normalize_LowercasesScheme()
    {
        // Arrange
        var url = "HTTPS://example.com/path";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.StartsWith("https://", result);
    }

    [Fact]
    public void Normalize_LowercasesHost()
    {
        // Arrange
        var url = "https://EXAMPLE.COM/path";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("example.com", result);
    }

    [Fact]
    public void Normalize_PreservesPathCase()
    {
        // Arrange
        var url = "https://example.com/Recipe/ChocolateCake";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("/Recipe/ChocolateCake", result);
    }

    [Fact]
    public void Normalize_RemovesTrailingSlash()
    {
        // Arrange
        var url = "https://example.com/recipes/";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/recipes", result);
    }

    [Fact]
    public void Normalize_PreservesRootSlash()
    {
        // Arrange
        var url = "https://example.com/";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/", result);
    }

    [Fact]
    public void Normalize_RemovesDefaultHttpPort()
    {
        // Arrange
        var url = "http://example.com:80/path";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("http://example.com/path", result);
    }

    [Fact]
    public void Normalize_RemovesDefaultHttpsPort()
    {
        // Arrange
        var url = "https://example.com:443/path";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/path", result);
    }

    [Fact]
    public void Normalize_PreservesNonDefaultPort()
    {
        // Arrange
        var url = "https://example.com:8080/path";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com:8080/path", result);
    }

    #endregion

    #region Query Parameter Sorting

    [Fact]
    public void Normalize_SortsQueryParameters()
    {
        // Arrange
        var url = "https://example.com/search?z=last&a=first&m=middle";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/search?a=first&m=middle&z=last", result);
    }

    [Fact]
    public void Normalize_SortsQueryParametersCaseInsensitive()
    {
        // Arrange
        var url = "https://example.com/search?Zebra=1&apple=2";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("apple=2", result);
        Assert.Contains("Zebra=1", result);
        Assert.True(result.IndexOf("apple") < result.IndexOf("Zebra"));
    }

    [Fact]
    public void Normalize_HandlesDuplicateQueryParameters()
    {
        // Arrange
        var url = "https://example.com/search?tag=b&tag=a";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("tag=a", result);
        Assert.Contains("tag=b", result);
    }

    [Fact]
    public void Normalize_HandlesEmptyQueryValue()
    {
        // Arrange
        var url = "https://example.com/search?empty=&filled=value";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("empty=", result);
        Assert.Contains("filled=value", result);
    }

    #endregion

    #region Tracking Parameter Removal

    [Theory]
    [InlineData("utm_source")]
    [InlineData("utm_medium")]
    [InlineData("utm_campaign")]
    [InlineData("utm_term")]
    [InlineData("utm_content")]
    [InlineData("utm_id")]
    [InlineData("UTM_SOURCE")] // Case insensitive
    public void Normalize_RemovesUtmParameters(string paramName)
    {
        // Arrange
        var url = $"https://example.com/page?{paramName}=test&keep=this";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.DoesNotContain(paramName, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep=this", result);
    }

    [Theory]
    [InlineData("fbclid")]
    [InlineData("gclid")]
    [InlineData("msclkid")]
    [InlineData("twclid")]
    [InlineData("mc_cid")]
    [InlineData("mc_eid")]
    public void Normalize_RemovesCommonTrackingParameters(string paramName)
    {
        // Arrange
        var url = $"https://example.com/page?{paramName}=abc123&id=42";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.DoesNotContain(paramName, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=42", result);
    }

    [Fact]
    public void Normalize_RemovesMultipleTrackingParameters()
    {
        // Arrange
        var url = "https://example.com/recipe?id=123&utm_source=google&fbclid=abc&category=dessert&gclid=xyz";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/recipe?category=dessert&id=123", result);
    }

    [Fact]
    public void Normalize_PreservesNonTrackingParameters()
    {
        // Arrange
        var url = "https://example.com/search?q=chocolate+cake&page=2&sort=rating";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("q=", result);
        Assert.Contains("page=2", result);
        Assert.Contains("sort=rating", result);
    }

    [Fact]
    public void Normalize_CanDisableTrackingRemoval()
    {
        // Arrange
        var url = "https://example.com/page?utm_source=newsletter&id=42";

        // Act
        var result = UrlNormalizer.Normalize(url, removeTrackingParams: false);

        // Assert
        Assert.Contains("utm_source=newsletter", result);
        Assert.Contains("id=42", result);
    }

    [Fact]
    public void Normalize_RemovesCustomUtmParameters()
    {
        // Arrange - utm_custom_param should still be removed due to utm_ prefix
        var url = "https://example.com/page?utm_custom_param=test&keep=this";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.DoesNotContain("utm_", result);
        Assert.Contains("keep=this", result);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Normalize_ThrowsOnNullUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize(null!));
    }

    [Fact]
    public void Normalize_ThrowsOnEmptyUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize(""));
    }

    [Fact]
    public void Normalize_ThrowsOnWhitespaceUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize("   "));
    }

    [Fact]
    public void Normalize_ThrowsOnInvalidUrl()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize("not-a-valid-url"));
    }

    [Fact]
    public void Normalize_ThrowsOnFtpScheme()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize("ftp://example.com/file"));
        Assert.Contains("Only http and https", ex.Message);
    }

    [Fact]
    public void Normalize_ThrowsOnFileScheme()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlNormalizer.Normalize("file:///path/to/file"));
    }

    [Fact]
    public void Normalize_HandlesUrlWithoutQueryString()
    {
        // Arrange
        var url = "https://example.com/recipes/chocolate-cake";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/recipes/chocolate-cake", result);
    }

    [Fact]
    public void Normalize_HandlesUrlWithOnlyTrackingParams()
    {
        // Arrange
        var url = "https://example.com/page?utm_source=test&fbclid=abc";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/page", result);
        Assert.DoesNotContain("?", result);
    }

    [Fact]
    public void Normalize_RemovesFragment()
    {
        // Arrange - fragments are client-side only and should not be included
        var url = "https://example.com/page#section";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Equal("https://example.com/page", result);
        Assert.DoesNotContain("#", result);
    }

    [Fact]
    public void Normalize_HandlesEncodedCharactersInPath()
    {
        // Arrange
        var url = "https://example.com/recipes/mac%20and%20cheese";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("/recipes/mac%20and%20cheese", result);
    }

    [Fact]
    public void Normalize_HandlesSpecialCharactersInQueryValue()
    {
        // Arrange
        var url = "https://example.com/search?q=mac+%26+cheese";

        // Act
        var result = UrlNormalizer.Normalize(url);

        // Assert
        Assert.Contains("q=mac", result);
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void Normalize_ProducesSameResultForEquivalentUrls()
    {
        // Arrange
        var url1 = "HTTPS://EXAMPLE.COM/recipes/?utm_source=test&id=42";
        var url2 = "https://example.com/recipes?id=42&utm_source=test";
        var url3 = "https://example.com:443/recipes?id=42&fbclid=abc";

        // Act
        var result1 = UrlNormalizer.Normalize(url1);
        var result2 = UrlNormalizer.Normalize(url2);
        var result3 = UrlNormalizer.Normalize(url3);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        // Arrange
        var url = "https://example.com/recipes?b=2&a=1";

        // Act
        var result1 = UrlNormalizer.Normalize(url);
        var result2 = UrlNormalizer.Normalize(result1);
        var result3 = UrlNormalizer.Normalize(result2);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    #endregion
}
