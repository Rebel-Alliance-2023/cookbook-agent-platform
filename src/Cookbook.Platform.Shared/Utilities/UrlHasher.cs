using System.Security.Cryptography;
using System.Text;

namespace Cookbook.Platform.Shared.Utilities;

/// <summary>
/// Generates URL-safe hashes for normalized URLs, used for duplicate detection.
/// </summary>
public static class UrlHasher
{
    /// <summary>
    /// The length of the hash string to return (first 22 characters of base64url).
    /// 22 base64 characters = 132 bits of entropy, sufficient for duplicate detection.
    /// </summary>
    public const int HashLength = 22;

    /// <summary>
    /// Computes a base64url-encoded SHA256 hash of the normalized URL.
    /// Returns the first 22 characters for a compact, URL-safe identifier.
    /// </summary>
    /// <param name="url">The URL to hash. Will be normalized before hashing.</param>
    /// <param name="normalizeFirst">Whether to normalize the URL before hashing. Default is true.</param>
    /// <returns>A 22-character base64url-encoded hash string.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or invalid.</exception>
    public static string ComputeHash(string url, bool normalizeFirst = true)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        // Normalize the URL first to ensure consistent hashing
        var urlToHash = normalizeFirst ? UrlNormalizer.Normalize(url) : url;

        // Compute SHA256 hash
        var urlBytes = Encoding.UTF8.GetBytes(urlToHash);
        var hashBytes = SHA256.HashData(urlBytes);

        // Convert to base64url encoding
        var base64 = Convert.ToBase64String(hashBytes);
        var base64Url = ToBase64Url(base64);

        // Return first 22 characters
        return base64Url[..HashLength];
    }

    /// <summary>
    /// Computes a hash from an already-normalized URL string.
    /// Use this when you've already normalized the URL to avoid double normalization.
    /// </summary>
    /// <param name="normalizedUrl">The pre-normalized URL to hash.</param>
    /// <returns>A 22-character base64url-encoded hash string.</returns>
    public static string ComputeHashFromNormalized(string normalizedUrl)
    {
        return ComputeHash(normalizedUrl, normalizeFirst: false);
    }

    /// <summary>
    /// Converts a standard base64 string to base64url encoding.
    /// Replaces '+' with '-', '/' with '_', and removes padding '='.
    /// </summary>
    private static string ToBase64Url(string base64)
    {
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
