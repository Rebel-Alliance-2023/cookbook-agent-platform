using System.Net;
using System.Net.Sockets;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Provides SSRF (Server-Side Request Forgery) protection by blocking requests to private/internal networks.
/// </summary>
public interface ISsrfProtectionService
{
    /// <summary>
    /// Validates that a URL's host does not resolve to a private/internal IP address.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with success status and error details.</returns>
    Task<SsrfValidationResult> ValidateUrlAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if an IP address is in a private/blocked range.
    /// </summary>
    /// <param name="ipAddress">The IP address to check.</param>
    /// <returns>True if the IP is private/blocked, false if public.</returns>
    bool IsPrivateOrBlockedIp(IPAddress ipAddress);
}

/// <summary>
/// Result of SSRF validation.
/// </summary>
public record SsrfValidationResult
{
    public required bool IsAllowed { get; init; }
    public string? BlockReason { get; init; }
    public IPAddress[]? ResolvedAddresses { get; init; }
    
    public static SsrfValidationResult Allowed(IPAddress[]? addresses = null) => new()
    {
        IsAllowed = true,
        ResolvedAddresses = addresses
    };
    
    public static SsrfValidationResult Blocked(string reason) => new()
    {
        IsAllowed = false,
        BlockReason = reason
    };
}

/// <summary>
/// Implementation of SSRF protection service.
/// </summary>
public class SsrfProtectionService : ISsrfProtectionService
{
    /// <summary>
    /// Validates that a URL's host does not resolve to a private/internal IP address.
    /// </summary>
    public async Task<SsrfValidationResult> ValidateUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return SsrfValidationResult.Blocked("Invalid URL format");
        }

        // Validate scheme
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return SsrfValidationResult.Blocked($"Invalid scheme: {uri.Scheme}. Only HTTP and HTTPS are allowed.");
        }

        var host = uri.Host;

        // Check if host is already an IP address
        if (IPAddress.TryParse(host, out var directIp))
        {
            if (IsPrivateOrBlockedIp(directIp))
            {
                return SsrfValidationResult.Blocked($"Direct IP address {directIp} is in a blocked range");
            }
            return SsrfValidationResult.Allowed([directIp]);
        }

        // Resolve hostname to IP addresses
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException ex)
        {
            return SsrfValidationResult.Blocked($"DNS resolution failed: {ex.Message}");
        }

        if (addresses.Length == 0)
        {
            return SsrfValidationResult.Blocked("DNS resolution returned no addresses");
        }

        // Check all resolved addresses
        foreach (var address in addresses)
        {
            if (IsPrivateOrBlockedIp(address))
            {
                return SsrfValidationResult.Blocked($"Resolved IP {address} is in a blocked range");
            }
        }

        return SsrfValidationResult.Allowed(addresses);
    }

    /// <summary>
    /// Checks if an IP address is in a private/blocked range.
    /// Blocks: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8, 169.254.0.0/16, ::1, link-local
    /// </summary>
    public bool IsPrivateOrBlockedIp(IPAddress ipAddress)
    {
        // Handle IPv4
        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ipAddress.GetAddressBytes();
            
            // 127.0.0.0/8 - Loopback
            if (bytes[0] == 127)
                return true;
            
            // 10.0.0.0/8 - Private Class A
            if (bytes[0] == 10)
                return true;
            
            // 172.16.0.0/12 - Private Class B (172.16.0.0 - 172.31.255.255)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            
            // 192.168.0.0/16 - Private Class C
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            
            // 169.254.0.0/16 - Link-local
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
            
            // 0.0.0.0/8 - Current network
            if (bytes[0] == 0)
                return true;
            
            // 100.64.0.0/10 - Carrier-grade NAT
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                return true;
            
            // 192.0.0.0/24 - IETF Protocol Assignments
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
                return true;
            
            // 192.0.2.0/24 - TEST-NET-1
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
                return true;
            
            // 198.51.100.0/24 - TEST-NET-2
            if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                return true;
            
            // 203.0.113.0/24 - TEST-NET-3
            if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                return true;
            
            // 224.0.0.0/4 - Multicast
            if (bytes[0] >= 224 && bytes[0] <= 239)
                return true;
            
            // 240.0.0.0/4 - Reserved for future use
            if (bytes[0] >= 240)
                return true;
            
            return false;
        }
        
        // Handle IPv6
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // ::1 - Loopback
            if (IPAddress.IsLoopback(ipAddress))
                return true;
            
            // fe80::/10 - Link-local
            if (ipAddress.IsIPv6LinkLocal)
                return true;
            
            // fc00::/7 - Unique local address (ULA)
            var bytes = ipAddress.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
                return true;
            
            // :: - Unspecified address
            if (ipAddress.Equals(IPAddress.IPv6None) || ipAddress.Equals(IPAddress.IPv6Any))
                return true;
            
            // ::ffff:0:0/96 - IPv4-mapped addresses (check the mapped IPv4)
            if (ipAddress.IsIPv4MappedToIPv6)
            {
                var ipv4 = ipAddress.MapToIPv4();
                return IsPrivateOrBlockedIp(ipv4);
            }
            
            return false;
        }
        
        // Block unknown address families
        return true;
    }
}
