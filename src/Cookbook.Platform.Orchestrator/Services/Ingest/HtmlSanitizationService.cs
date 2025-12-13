using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Implementation of ISanitizationService that cleans HTML and extracts structured data.
/// Uses regex-based parsing for portability (no external HTML library dependency).
/// </summary>
public partial class HtmlSanitizationService : ISanitizationService
{
    private readonly ILogger<HtmlSanitizationService> _logger;

    // Elements to completely remove (including content)
    private static readonly HashSet<string> RemoveElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "iframe", "object", "embed", "applet",
        "nav", "header", "footer", "aside", "menu", "menuitem",
        "template", "svg", "canvas", "video", "audio", "source", "track",
        "form", "input", "button", "select", "textarea", "option", "optgroup",
        "advertisement", "ad", "banner"
    };

    // Elements that typically contain non-recipe navigation content
    private static readonly HashSet<string> NavigationClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "nav", "navbar", "navigation", "menu", "sidebar", "footer", "header",
        "breadcrumb", "breadcrumbs", "pagination", "social", "share", "sharing",
        "advertisement", "ad", "ads", "banner", "promo", "promotion",
        "newsletter", "subscribe", "subscription", "signup", "sign-up",
        "comment", "comments", "related", "recommended", "popular", "trending"
    };

    // Schema.org types that represent recipes
    private static readonly HashSet<string> RecipeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Recipe", "HowTo", "https://schema.org/Recipe", "http://schema.org/Recipe"
    };

    public HtmlSanitizationService(ILogger<HtmlSanitizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sanitizes HTML content by removing scripts, styles, and non-content elements.
    /// </summary>
    public SanitizedContent Sanitize(string html, string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new SanitizedContent
            {
                TextContent = string.Empty,
                OriginalLength = 0,
                SanitizedLength = 0
            };
        }

        var originalLength = html.Length;
        _logger.LogDebug("Sanitizing HTML content: {Length} characters", originalLength);

        // Extract JSON-LD before removing script tags
        var jsonLdSnippets = ExtractJsonLd(html);
        var recipeJsonLd = FindRecipeJsonLd(jsonLdSnippets);

        // Extract metadata
        var metadata = ExtractMetadata(html);

        // Remove unwanted elements
        var cleaned = RemoveUnwantedElements(html);

        // Convert to plain text
        var textContent = ConvertToPlainText(cleaned);

        // Normalize whitespace
        textContent = NormalizeWhitespace(textContent);

        _logger.LogDebug("Sanitization complete: {Original} -> {Sanitized} characters, {JsonLd} JSON-LD snippets",
            originalLength, textContent.Length, jsonLdSnippets.Count);

        return new SanitizedContent
        {
            TextContent = textContent,
            JsonLdSnippets = jsonLdSnippets,
            RecipeJsonLd = recipeJsonLd,
            Metadata = metadata,
            OriginalLength = originalLength,
            SanitizedLength = textContent.Length
        };
    }

    /// <summary>
    /// Extracts all JSON-LD snippets from script tags.
    /// </summary>
    public List<string> ExtractJsonLd(string html)
    {
        var snippets = new List<string>();

        if (string.IsNullOrWhiteSpace(html))
            return snippets;

        try
        {
            // Match script tags with type="application/ld+json"
            var matches = JsonLdScriptRegex().Matches(html);

            foreach (Match match in matches)
            {
                if (match.Groups["content"].Success)
                {
                    var content = match.Groups["content"].Value.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Validate it's valid JSON
                        try
                        {
                            using var doc = JsonDocument.Parse(content);
                            snippets.Add(content);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogDebug(ex, "Invalid JSON in LD+JSON script tag");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting JSON-LD from HTML");
        }

        return snippets;
    }

    /// <summary>
    /// Filters JSON-LD snippets to find Recipe schema data.
    /// </summary>
    public string? FindRecipeJsonLd(List<string> jsonLdSnippets)
    {
        foreach (var snippet in jsonLdSnippets)
        {
            try
            {
                var recipe = FindRecipeInJsonLd(snippet);
                if (recipe != null)
                {
                    return recipe;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing JSON-LD snippet for recipe");
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively searches for Recipe type in JSON-LD structure.
    /// </summary>
    private string? FindRecipeInJsonLd(string jsonLd)
    {
        using var doc = JsonDocument.Parse(jsonLd);
        var root = doc.RootElement;

        // Handle @graph structure (array of items)
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("@graph", out var graph))
        {
            if (graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    if (IsRecipeType(item))
                    {
                        return item.GetRawText();
                    }
                }
            }
        }

        // Handle direct object
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (IsRecipeType(root))
            {
                return jsonLd;
            }
        }

        // Handle array of items
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (IsRecipeType(item))
                {
                    return item.GetRawText();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a JSON element represents a Recipe type.
    /// </summary>
    private static bool IsRecipeType(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!element.TryGetProperty("@type", out var typeProperty))
            return false;

        // Handle single type
        if (typeProperty.ValueKind == JsonValueKind.String)
        {
            var type = typeProperty.GetString();
            return type != null && RecipeTypes.Contains(type);
        }

        // Handle array of types
        if (typeProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var type in typeProperty.EnumerateArray())
            {
                if (type.ValueKind == JsonValueKind.String)
                {
                    var typeStr = type.GetString();
                    if (typeStr != null && RecipeTypes.Contains(typeStr))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts page metadata from HTML.
    /// </summary>
    private PageMetadata ExtractMetadata(string html)
    {
        string? title = null;
        string? description = null;
        string? author = null;
        string? siteName = null;
        string? canonicalUrl = null;
        string? language = null;

        // Extract title
        var titleMatch = TitleRegex().Match(html);
        if (titleMatch.Success)
            title = DecodeHtmlEntities(titleMatch.Groups["title"].Value.Trim());

        // Extract meta tags
        var metaMatches = MetaTagRegex().Matches(html);
        foreach (Match match in metaMatches)
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var property = match.Groups["property"].Value.ToLowerInvariant();
            var content = DecodeHtmlEntities(match.Groups["content"].Value);

            if (name == "description" || property == "og:description")
                description ??= content;
            if (name == "author")
                author = content;
            if (property == "og:site_name")
                siteName = content;
            if (property == "og:title" && string.IsNullOrEmpty(title))
                title = content;
        }

        // Extract canonical URL
        var canonicalMatch = CanonicalRegex().Match(html);
        if (canonicalMatch.Success)
            canonicalUrl = canonicalMatch.Groups["url"].Value;

        // Extract language
        var langMatch = LanguageRegex().Match(html);
        if (langMatch.Success)
            language = langMatch.Groups["lang"].Value;

        return new PageMetadata
        {
            Title = title,
            Description = description,
            Author = author,
            SiteName = siteName,
            CanonicalUrl = canonicalUrl,
            Language = language
        };
    }

    /// <summary>
    /// Removes unwanted HTML elements.
    /// </summary>
    private string RemoveUnwantedElements(string html)
    {
        var result = html;

        // Remove comments
        result = HtmlCommentRegex().Replace(result, " ");

        // Remove script, style, and other unwanted elements with their content
        foreach (var tag in RemoveElements)
        {
            var pattern = $@"<{tag}[^>]*>[\s\S]*?</{tag}>|<{tag}[^>]*/>";
            result = Regex.Replace(result, pattern, " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        // Remove elements with navigation-related classes
        foreach (var className in NavigationClasses)
        {
            var pattern = $@"<[^>]+class\s*=\s*[""'][^""']*\b{className}\b[^""']*[""'][^>]*>[\s\S]*?</[^>]+>";
            result = Regex.Replace(result, pattern, " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        return result;
    }

    /// <summary>
    /// Converts HTML to plain text.
    /// </summary>
    private string ConvertToPlainText(string html)
    {
        var result = html;

        // Replace block elements with newlines
        result = BlockElementRegex().Replace(result, "\n");

        // Replace br tags with newlines
        result = BrTagRegex().Replace(result, "\n");

        // Replace list items with bullet points
        result = ListItemRegex().Replace(result, "\n• ");

        // Remove all remaining HTML tags
        result = HtmlTagRegex().Replace(result, " ");

        // Decode HTML entities
        result = DecodeHtmlEntities(result);

        return result;
    }

    /// <summary>
    /// Normalizes whitespace in text.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder();
        var previousWasWhitespace = false;
        var previousWasNewline = false;

        foreach (var c in text)
        {
            if (c == '\n' || c == '\r')
            {
                if (!previousWasNewline)
                {
                    sb.Append('\n');
                    previousWasNewline = true;
                    previousWasWhitespace = true;
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                {
                    sb.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                sb.Append(c);
                previousWasWhitespace = false;
                previousWasNewline = false;
            }
        }

        // Collapse multiple newlines
        var result = Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n");

        return result.Trim();
    }

    /// <summary>
    /// Decodes common HTML entities.
    /// </summary>
    private static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = text;

        // Named entities
        result = result.Replace("&nbsp;", " ");
        result = result.Replace("&amp;", "&");
        result = result.Replace("&lt;", "<");
        result = result.Replace("&gt;", ">");
        result = result.Replace("&quot;", "\"");
        result = result.Replace("&apos;", "'");
        result = result.Replace("&#39;", "'");
        result = result.Replace("&mdash;", "—");
        result = result.Replace("&ndash;", "–");
        result = result.Replace("&hellip;", "...");
        result = result.Replace("&copy;", "©");
        result = result.Replace("&reg;", "®");
        result = result.Replace("&trade;", "™");
        result = result.Replace("&deg;", "°");
        result = result.Replace("&frac12;", "½");
        result = result.Replace("&frac14;", "¼");
        result = result.Replace("&frac34;", "¾");

        // Numeric entities
        result = NumericEntityRegex().Replace(result, m =>
        {
            var code = int.Parse(m.Groups["code"].Value);
            return ((char)code).ToString();
        });

        result = HexEntityRegex().Replace(result, m =>
        {
            var code = Convert.ToInt32(m.Groups["code"].Value, 16);
            return ((char)code).ToString();
        });

        return result;
    }

    #region Compiled Regex Patterns

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(?<content>[\s\S]*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JsonLdScriptRegex();

    [GeneratedRegex(@"<title[^>]*>(?<title>[\s\S]*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<meta[^>]*(?:name\s*=\s*[""'](?<name>[^""']*)[""']|property\s*=\s*[""'](?<property>[^""']*)[""'])[^>]*content\s*=\s*[""'](?<content>[^""']*)[""'][^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"<link[^>]*rel\s*=\s*[""']canonical[""'][^>]*href\s*=\s*[""'](?<url>[^""']*)[""'][^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"<html[^>]*lang\s*=\s*[""'](?<lang>[^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LanguageRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.Compiled)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"<(?:div|p|h[1-6]|article|section|main|blockquote|pre|table|ul|ol|dl)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BlockElementRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"&#(?<code>\d+);", RegexOptions.Compiled)]
    private static partial Regex NumericEntityRegex();

    [GeneratedRegex(@"&#x(?<code>[0-9a-fA-F]+);", RegexOptions.Compiled)]
    private static partial Regex HexEntityRegex();

    #endregion
}
