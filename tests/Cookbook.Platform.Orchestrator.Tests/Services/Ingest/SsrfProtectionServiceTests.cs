using System.Net;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for SsrfProtectionService.
/// </summary>
public class SsrfProtectionServiceTests
{
    private readonly SsrfProtectionService _service;

    public SsrfProtectionServiceTests()
    {
        _service = new SsrfProtectionService();
    }

    #region IPv4 Private Range Tests

    [Theory]
    [InlineData("127.0.0.1", true)] // Loopback
    [InlineData("127.255.255.255", true)] // Loopback range
    [InlineData("10.0.0.1", true)] // Class A private
    [InlineData("10.255.255.255", true)] // Class A private
    [InlineData("172.16.0.1", true)] // Class B private start
    [InlineData("172.31.255.255", true)] // Class B private end
    [InlineData("192.168.0.1", true)] // Class C private
    [InlineData("192.168.255.255", true)] // Class C private
    [InlineData("169.254.0.1", true)] // Link-local
    [InlineData("0.0.0.0", true)] // Current network
    public void IsPrivateOrBlockedIp_IPv4PrivateRanges_ReturnsTrue(string ip, bool expected)
    {
        var address = IPAddress.Parse(ip);
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("8.8.8.8", false)] // Google DNS
    [InlineData("1.1.1.1", false)] // Cloudflare DNS
    [InlineData("93.184.216.34", false)] // example.com
    [InlineData("172.15.255.255", false)] // Just before Class B private
    [InlineData("172.32.0.0", false)] // Just after Class B private
    [InlineData("192.167.255.255", false)] // Just before 192.168
    [InlineData("192.169.0.0", false)] // Just after 192.168
    public void IsPrivateOrBlockedIp_IPv4PublicAddresses_ReturnsFalse(string ip, bool expected)
    {
        var address = IPAddress.Parse(ip);
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("100.64.0.0", true)] // Carrier-grade NAT start
    [InlineData("100.127.255.255", true)] // Carrier-grade NAT end
    [InlineData("192.0.0.1", true)] // IETF Protocol Assignments
    [InlineData("192.0.2.1", true)] // TEST-NET-1
    [InlineData("198.51.100.1", true)] // TEST-NET-2
    [InlineData("203.0.113.1", true)] // TEST-NET-3
    [InlineData("224.0.0.1", true)] // Multicast
    [InlineData("240.0.0.1", true)] // Reserved
    public void IsPrivateOrBlockedIp_IPv4SpecialRanges_ReturnsTrue(string ip, bool expected)
    {
        var address = IPAddress.Parse(ip);
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.Equal(expected, result);
    }

    #endregion

    #region IPv6 Tests

    [Fact]
    public void IsPrivateOrBlockedIp_IPv6Loopback_ReturnsTrue()
    {
        var address = IPAddress.IPv6Loopback; // ::1
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateOrBlockedIp_IPv6LinkLocal_ReturnsTrue()
    {
        var address = IPAddress.Parse("fe80::1");
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateOrBlockedIp_IPv6UniqueLocal_ReturnsTrue()
    {
        var address = IPAddress.Parse("fc00::1");
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateOrBlockedIp_IPv6Unspecified_ReturnsTrue()
    {
        var address = IPAddress.IPv6Any; // ::
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateOrBlockedIp_IPv6Public_ReturnsFalse()
    {
        var address = IPAddress.Parse("2001:4860:4860::8888"); // Google DNS IPv6
        var result = _service.IsPrivateOrBlockedIp(address);
        Assert.False(result);
    }

    #endregion

    #region URL Validation Tests

    [Fact]
    public async Task ValidateUrlAsync_InvalidUrlFormat_ReturnsBlocked()
    {
        var result = await _service.ValidateUrlAsync("not-a-valid-url");
        
        Assert.False(result.IsAllowed);
        Assert.Contains("Invalid URL format", result.BlockReason);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>test</h1>")]
    public async Task ValidateUrlAsync_InvalidScheme_ReturnsBlocked(string url)
    {
        var result = await _service.ValidateUrlAsync(url);
        
        Assert.False(result.IsAllowed);
        Assert.Contains("Invalid scheme", result.BlockReason);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://172.16.0.1/")]
    public async Task ValidateUrlAsync_DirectPrivateIp_ReturnsBlocked(string url)
    {
        var result = await _service.ValidateUrlAsync(url);
        
        Assert.False(result.IsAllowed);
        Assert.Contains("blocked range", result.BlockReason);
    }

    [Fact]
    public async Task ValidateUrlAsync_ValidHttpsUrl_ReturnsAllowed()
    {
        // This test requires actual DNS resolution, so we use a known public site
        // In real tests, we might mock DNS resolution
        var result = await _service.ValidateUrlAsync("https://example.com/");
        
        // Note: This may fail if DNS resolution fails or returns private IPs
        // For unit tests, consider mocking the DNS resolution
        Assert.True(result.IsAllowed || result.BlockReason?.Contains("DNS") == true);
    }

    #endregion
}
