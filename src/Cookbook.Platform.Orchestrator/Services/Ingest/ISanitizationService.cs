using System.Text.Json.Serialization;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Metadata extracted from an HTML page.
/// </summary>
public record PageMetadata
{
    /// <summary>
    /// The page title from the title tag or og:title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    
    /// <summary>
    /// The page description from meta description or og:description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    /// <summary>
    /// The author from meta author tag.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }
    
    /// <summary>
    /// The site name from og:site_name.
    /// </summary>
    [JsonPropertyName("siteName")]
    public string? SiteName { get; init; }
    
    /// <summary>
    /// The canonical URL if present.
    /// </summary>
    [JsonPropertyName("canonicalUrl")]
    public string? CanonicalUrl { get; init; }
    
    /// <summary>
    /// The language from the html lang attribute.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
}

/// <summary>
/// Result of HTML sanitization containing cleaned text and extracted data.
/// </summary>
public record SanitizedContent
{
    /// <summary>
    /// The cleaned plain text content with scripts, styles, and navigation removed.
    /// </summary>
    [JsonPropertyName("textContent")]
    public required string TextContent { get; init; }
    
    /// <summary>
    /// JSON-LD snippets extracted from the page (raw JSON strings).
    /// </summary>
    [JsonPropertyName("jsonLdSnippets")]
    public List<string> JsonLdSnippets { get; init; } = [];
    
    /// <summary>
    /// Recipe-specific JSON-LD data if found (Schema.org Recipe).
    /// </summary>
    [JsonPropertyName("recipeJsonLd")]
    public string? RecipeJsonLd { get; init; }
    
    /// <summary>
    /// Metadata extracted from the page.
    /// </summary>
    [JsonPropertyName("metadata")]
    public PageMetadata Metadata { get; init; } = new();
    
    /// <summary>
    /// Character count of the original HTML.
    /// </summary>
    [JsonPropertyName("originalLength")]
    public int OriginalLength { get; init; }
    
    /// <summary>
    /// Character count of the sanitized text.
    /// </summary>
    [JsonPropertyName("sanitizedLength")]
    public int SanitizedLength { get; init; }
    
    /// <summary>
    /// Whether a Recipe JSON-LD was found.
    /// </summary>
    [JsonPropertyName("hasRecipeJsonLd")]
    public bool HasRecipeJsonLd => !string.IsNullOrEmpty(RecipeJsonLd);
}

/// <summary>
/// Service for sanitizing HTML content and extracting structured data.
/// </summary>
public interface ISanitizationService
{
    /// <summary>
    /// Sanitizes HTML content by removing scripts, styles, and non-content elements,
    /// while extracting JSON-LD structured data and page metadata.
    /// </summary>
    /// <param name="html">The raw HTML content to sanitize.</param>
    /// <param name="sourceUrl">The source URL for context (optional).</param>
    /// <returns>The sanitized content with extracted data.</returns>
    SanitizedContent Sanitize(string html, string? sourceUrl = null);
    
    /// <summary>
    /// Extracts all JSON-LD snippets from HTML content.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>List of JSON-LD strings found in script tags.</returns>
    List<string> ExtractJsonLd(string html);
    
    /// <summary>
    /// Filters JSON-LD snippets to find Recipe schema data.
    /// </summary>
    /// <param name="jsonLdSnippets">The JSON-LD snippets to filter.</param>
    /// <returns>The Recipe JSON-LD if found, null otherwise.</returns>
    string? FindRecipeJsonLd(List<string> jsonLdSnippets);
}
