using System.Text;
using System.Web;

namespace Cookbook.Platform.Shared.Utilities;

/// <summary>
/// Normalizes URLs for consistent comparison and duplicate detection.
/// </summary>
public static class UrlNormalizer
{
    /// <summary>
    /// Known tracking parameters to remove from URLs.
    /// </summary>
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        // Google Analytics
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "utm_id",
        "utm_source_platform",
        "utm_creative_format",
        "utm_marketing_tactic",
        
        // Facebook
        "fbclid",
        "fb_action_ids",
        "fb_action_types",
        "fb_source",
        "fb_ref",
        
        // Microsoft/Bing
        "msclkid",
        
        // Google Ads
        "gclid",
        "gclsrc",
        "dclid",
        
        // Twitter
        "twclid",
        
        // HubSpot
        "hsa_acc",
        "hsa_cam",
        "hsa_grp",
        "hsa_ad",
        "hsa_src",
        "hsa_tgt",
        "hsa_kw",
        "hsa_mt",
        "hsa_net",
        "hsa_ver",
        
        // Mailchimp
        "mc_cid",
        "mc_eid",
        
        // Other common tracking
        "ref",
        "ref_src",
        "ref_url",
        "_ga",
        "_gl",
        "oly_enc_id",
        "oly_anon_id",
        "vero_id",
        "nr_email_referer",
        "mkt_tok"
    };

    /// <summary>
    /// Normalizes a URL by lowercasing scheme and host, removing trailing slashes,
    /// sorting query parameters, and optionally removing tracking parameters.
    /// </summary>
    /// <param name="url">The URL to normalize.</param>
    /// <param name="removeTrackingParams">Whether to remove known tracking parameters.</param>
    /// <returns>The normalized URL string.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or invalid.</exception>
    public static string Normalize(string url, bool removeTrackingParams = true)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url));
        }

        // Only process http and https schemes
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Only http and https schemes are supported. Got: {uri.Scheme}", nameof(url));
        }

        var builder = new StringBuilder();

        // Lowercase scheme
        builder.Append(uri.Scheme.ToLowerInvariant());
        builder.Append("://");

        // Lowercase host
        builder.Append(uri.Host.ToLowerInvariant());

        // Include port only if non-default
        if (!uri.IsDefaultPort)
        {
            builder.Append(':');
            builder.Append(uri.Port);
        }

        // Normalize path - remove trailing slash unless it's the root
        var path = uri.AbsolutePath;
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }
        builder.Append(path);

        // Process and sort query parameters
        var queryParams = ParseAndSortQueryParams(uri.Query, removeTrackingParams);
        if (queryParams.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")));
        }

        // Fragment is typically not included in normalization for duplicate detection
        // as fragments are client-side only

        return builder.ToString();
    }

    /// <summary>
    /// Parses query string and returns sorted key-value pairs,
    /// optionally filtering out tracking parameters.
    /// </summary>
    private static List<KeyValuePair<string, string>> ParseAndSortQueryParams(
        string query, 
        bool removeTrackingParams)
    {
        var result = new List<KeyValuePair<string, string>>();

        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        // Remove leading '?' if present
        if (query.StartsWith('?'))
        {
            query = query[1..];
        }

        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var nameValueCollection = HttpUtility.ParseQueryString(query);

        foreach (string? key in nameValueCollection.AllKeys)
        {
            if (key is null)
            {
                continue;
            }

            // Skip tracking parameters if configured
            if (removeTrackingParams && IsTrackingParameter(key))
            {
                continue;
            }

            var values = nameValueCollection.GetValues(key);
            if (values is not null)
            {
                foreach (var value in values)
                {
                    result.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
                }
            }
        }

        // Sort by key, then by value for deterministic output
        return result
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(kvp => kvp.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Checks if a query parameter is a known tracking parameter.
    /// Supports prefix matching for utm_* style parameters.
    /// </summary>
    private static bool IsTrackingParameter(string paramName)
    {
        // Direct match
        if (TrackingParameters.Contains(paramName))
        {
            return true;
        }

        // Prefix match for utm_ parameters (catches any utm_* variant)
        if (paramName.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
