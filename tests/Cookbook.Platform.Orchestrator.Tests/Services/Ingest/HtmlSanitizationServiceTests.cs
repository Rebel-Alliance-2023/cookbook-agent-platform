using Cookbook.Platform.Orchestrator.Services.Ingest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for HtmlSanitizationService.
/// </summary>
public class HtmlSanitizationServiceTests
{
    private readonly HtmlSanitizationService _service;

    public HtmlSanitizationServiceTests()
    {
        var loggerMock = new Mock<ILogger<HtmlSanitizationService>>();
        _service = new HtmlSanitizationService(loggerMock.Object);
    }

    #region Basic Sanitization Tests

    [Fact]
    public void Sanitize_EmptyHtml_ReturnsEmptyContent()
    {
        var result = _service.Sanitize("");

        Assert.Empty(result.TextContent);
        Assert.Equal(0, result.OriginalLength);
        Assert.Equal(0, result.SanitizedLength);
    }

    [Fact]
    public void Sanitize_NullHtml_ReturnsEmptyContent()
    {
        var result = _service.Sanitize(null!);

        Assert.Empty(result.TextContent);
    }

    [Fact]
    public void Sanitize_PlainText_ReturnsUnchanged()
    {
        var html = "This is plain text content";
        
        var result = _service.Sanitize(html);

        Assert.Equal("This is plain text content", result.TextContent);
    }

    [Fact]
    public void Sanitize_SimpleHtml_ExtractsText()
    {
        var html = "<html><body><p>Hello World</p></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Hello World", result.TextContent);
    }

    [Fact]
    public void Sanitize_PreservesHeadings()
    {
        var html = "<h1>Main Title</h1><h2>Subtitle</h2><p>Content</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Main Title", result.TextContent);
        Assert.Contains("Subtitle", result.TextContent);
        Assert.Contains("Content", result.TextContent);
    }

    [Fact]
    public void Sanitize_PreservesListItems()
    {
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Item 1", result.TextContent);
        Assert.Contains("Item 2", result.TextContent);
        Assert.Contains("Item 3", result.TextContent);
        Assert.Contains("•", result.TextContent); // Bullet points
    }

    #endregion

    #region Script and Style Removal Tests

    [Fact]
    public void Sanitize_RemovesScriptTags()
    {
        var html = "<p>Before</p><script>alert('evil');</script><p>After</p>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("alert", result.TextContent);
        Assert.DoesNotContain("evil", result.TextContent);
        Assert.Contains("Before", result.TextContent);
        Assert.Contains("After", result.TextContent);
    }

    [Fact]
    public void Sanitize_RemovesStyleTags()
    {
        var html = "<p>Content</p><style>.class { color: red; }</style>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("color", result.TextContent);
        Assert.DoesNotContain("red", result.TextContent);
        Assert.Contains("Content", result.TextContent);
    }

    [Fact]
    public void Sanitize_RemovesInlineScripts()
    {
        var html = @"<script type=""text/javascript"">
            var x = 1;
            console.log(x);
        </script><p>Visible</p>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("console", result.TextContent);
        Assert.Contains("Visible", result.TextContent);
    }

    [Fact]
    public void Sanitize_RemovesNoscript()
    {
        var html = "<noscript>JavaScript is disabled</noscript><p>Content</p>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("JavaScript is disabled", result.TextContent);
        Assert.Contains("Content", result.TextContent);
    }

    #endregion

    #region Navigation Element Removal Tests

    [Fact]
    public void Sanitize_RemovesNavElements()
    {
        var html = "<nav><a href='/'>Home</a><a href='/about'>About</a></nav><p>Main content</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Main content", result.TextContent);
        // Nav content should be removed
    }

    [Fact]
    public void Sanitize_RemovesFooterElements()
    {
        var html = "<article>Article content</article><footer>Copyright 2024</footer>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Article content", result.TextContent);
    }

    [Fact]
    public void Sanitize_RemovesIframes()
    {
        var html = "<p>Content</p><iframe src='https://evil.com'></iframe>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("iframe", result.TextContent);
        Assert.DoesNotContain("evil.com", result.TextContent);
    }

    #endregion

    #region HTML Entity Decoding Tests

    [Fact]
    public void Sanitize_DecodesHtmlEntities()
    {
        var html = "<p>Tom &amp; Jerry</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("Tom & Jerry", result.TextContent);
    }

    [Fact]
    public void Sanitize_DecodesQuotes()
    {
        var html = "<p>&quot;Hello&quot; and &apos;World&apos;</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("\"Hello\"", result.TextContent);
        Assert.Contains("'World'", result.TextContent);
    }

    [Fact]
    public void Sanitize_DecodesFractions()
    {
        var html = "<p>&frac12; cup flour, &frac14; cup sugar</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("½ cup flour", result.TextContent);
        Assert.Contains("¼ cup sugar", result.TextContent);
    }

    [Fact]
    public void Sanitize_DecodesNumericEntities()
    {
        var html = "<p>&#169; 2024</p>";
        
        var result = _service.Sanitize(html);

        Assert.Contains("© 2024", result.TextContent);
    }

    #endregion

    #region Whitespace Normalization Tests

    [Fact]
    public void Sanitize_NormalizesWhitespace()
    {
        var html = "<p>Multiple    spaces     here</p>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("    ", result.TextContent);
        Assert.Contains("Multiple spaces here", result.TextContent);
    }

    [Fact]
    public void Sanitize_CollapsesNewlines()
    {
        var html = "<p>Line 1</p>\n\n\n\n\n<p>Line 2</p>";
        
        var result = _service.Sanitize(html);

        Assert.DoesNotContain("\n\n\n", result.TextContent);
    }

    [Fact]
    public void Sanitize_TrimsResult()
    {
        var html = "   <p>Content</p>   ";
        
        var result = _service.Sanitize(html);

        Assert.False(result.TextContent.StartsWith(" "));
        Assert.False(result.TextContent.EndsWith(" "));
    }

    #endregion

    #region Metadata Extraction Tests

    [Fact]
    public void Sanitize_ExtractsTitle()
    {
        var html = "<html><head><title>Recipe Page Title</title></head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("Recipe Page Title", result.Metadata.Title);
    }

    [Fact]
    public void Sanitize_ExtractsDescription()
    {
        var html = @"<html><head>
            <meta name=""description"" content=""A delicious recipe"">
        </head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("A delicious recipe", result.Metadata.Description);
    }

    [Fact]
    public void Sanitize_ExtractsAuthor()
    {
        var html = @"<html><head>
            <meta name=""author"" content=""Chef John"">
        </head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("Chef John", result.Metadata.Author);
    }

    [Fact]
    public void Sanitize_ExtractsOgSiteName()
    {
        var html = @"<html><head>
            <meta property=""og:site_name"" content=""Cooking Blog"">
        </head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("Cooking Blog", result.Metadata.SiteName);
    }

    [Fact]
    public void Sanitize_ExtractsCanonicalUrl()
    {
        var html = @"<html><head>
            <link rel=""canonical"" href=""https://example.com/recipe"">
        </head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("https://example.com/recipe", result.Metadata.CanonicalUrl);
    }

    [Fact]
    public void Sanitize_ExtractsLanguage()
    {
        var html = @"<html lang=""en-US""><head></head><body></body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Equal("en-US", result.Metadata.Language);
    }

    #endregion

    #region Content Statistics Tests

    [Fact]
    public void Sanitize_TracksOriginalLength()
    {
        var html = "<p>Short content</p>";
        
        var result = _service.Sanitize(html);

        Assert.Equal(html.Length, result.OriginalLength);
    }

    [Fact]
    public void Sanitize_TracksSanitizedLength()
    {
        var html = "<p>Content</p>";
        
        var result = _service.Sanitize(html);

        Assert.Equal(result.TextContent.Length, result.SanitizedLength);
    }

    #endregion
}

/// <summary>
/// Unit tests for JSON-LD extraction.
/// </summary>
public class JsonLdExtractionTests
{
    private readonly HtmlSanitizationService _service;

    public JsonLdExtractionTests()
    {
        var loggerMock = new Mock<ILogger<HtmlSanitizationService>>();
        _service = new HtmlSanitizationService(loggerMock.Object);
    }

    #region ExtractJsonLd Tests

    [Fact]
    public void ExtractJsonLd_NoScriptTags_ReturnsEmpty()
    {
        var html = "<html><body><p>No JSON-LD here</p></body></html>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractJsonLd_NonJsonLdScript_ReturnsEmpty()
    {
        var html = @"<script type=""text/javascript"">alert('hi');</script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractJsonLd_ValidJsonLd_ReturnsContent()
    {
        var html = @"<script type=""application/ld+json"">{""@type"": ""Organization""}</script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Single(result);
        Assert.Contains("Organization", result[0]);
    }

    [Fact]
    public void ExtractJsonLd_MultipleJsonLdBlocks_ReturnsAll()
    {
        var html = @"
            <script type=""application/ld+json"">{""@type"": ""Organization""}</script>
            <script type=""application/ld+json"">{""@type"": ""Recipe""}</script>
            <script type=""application/ld+json"">{""@type"": ""WebPage""}</script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ExtractJsonLd_InvalidJson_SkipsInvalid()
    {
        var html = @"
            <script type=""application/ld+json"">not valid json</script>
            <script type=""application/ld+json"">{""@type"": ""Recipe""}</script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Single(result);
        Assert.Contains("Recipe", result[0]);
    }

    [Fact]
    public void ExtractJsonLd_EmptyScript_SkipsEmpty()
    {
        var html = @"<script type=""application/ld+json"">   </script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractJsonLd_PreservesJsonLdContent_DoesNotRemove()
    {
        var recipeJson = @"{
            ""@context"": ""https://schema.org"",
            ""@type"": ""Recipe"",
            ""name"": ""Chocolate Cake"",
            ""recipeIngredient"": [""1 cup flour"", ""2 eggs""]
        }";
        var html = $@"<script type=""application/ld+json"">{recipeJson}</script>";
        
        var result = _service.ExtractJsonLd(html);

        Assert.Single(result);
        Assert.Contains("Chocolate Cake", result[0]);
        Assert.Contains("recipeIngredient", result[0]);
    }

    #endregion

    #region FindRecipeJsonLd Tests

    [Fact]
    public void FindRecipeJsonLd_NoSnippets_ReturnsNull()
    {
        var result = _service.FindRecipeJsonLd(new List<string>());

        Assert.Null(result);
    }

    [Fact]
    public void FindRecipeJsonLd_NoRecipeType_ReturnsNull()
    {
        var snippets = new List<string>
        {
            @"{""@type"": ""Organization"", ""name"": ""Company""}",
            @"{""@type"": ""WebPage"", ""name"": ""Page""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.Null(result);
    }

    [Fact]
    public void FindRecipeJsonLd_DirectRecipeType_ReturnsRecipe()
    {
        var snippets = new List<string>
        {
            @"{""@type"": ""Recipe"", ""name"": ""Chocolate Cake""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("Chocolate Cake", result);
    }

    [Fact]
    public void FindRecipeJsonLd_RecipeInGraph_ReturnsRecipe()
    {
        var snippets = new List<string>
        {
            @"{
                ""@context"": ""https://schema.org"",
                ""@graph"": [
                    {""@type"": ""WebPage"", ""name"": ""Page""},
                    {""@type"": ""Recipe"", ""name"": ""Pasta Recipe""},
                    {""@type"": ""Organization"", ""name"": ""Site""}
                ]
            }"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("Pasta Recipe", result);
    }

    [Fact]
    public void FindRecipeJsonLd_RecipeInArray_ReturnsRecipe()
    {
        var snippets = new List<string>
        {
            @"[
                {""@type"": ""WebPage"", ""name"": ""Page""},
                {""@type"": ""Recipe"", ""name"": ""Soup Recipe""}
            ]"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("Soup Recipe", result);
    }

    [Fact]
    public void FindRecipeJsonLd_ArrayOfTypes_MatchesRecipe()
    {
        var snippets = new List<string>
        {
            @"{""@type"": [""CreativeWork"", ""Recipe""], ""name"": ""Multi-Type Recipe""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("Multi-Type Recipe", result);
    }

    [Fact]
    public void FindRecipeJsonLd_HowToType_MatchesRecipe()
    {
        var snippets = new List<string>
        {
            @"{""@type"": ""HowTo"", ""name"": ""How to make bread""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("How to make bread", result);
    }

    [Fact]
    public void FindRecipeJsonLd_FullSchemaOrgUrl_MatchesRecipe()
    {
        var snippets = new List<string>
        {
            @"{""@type"": ""https://schema.org/Recipe"", ""name"": ""URL Type Recipe""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("URL Type Recipe", result);
    }

    [Fact]
    public void FindRecipeJsonLd_SelectsFirstRecipe_WhenMultiple()
    {
        var snippets = new List<string>
        {
            @"{""@type"": ""Recipe"", ""name"": ""First Recipe""}",
            @"{""@type"": ""Recipe"", ""name"": ""Second Recipe""}"
        };
        
        var result = _service.FindRecipeJsonLd(snippets);

        Assert.NotNull(result);
        Assert.Contains("First Recipe", result);
    }

    #endregion

    #region Sanitize Integration Tests

    [Fact]
    public void Sanitize_ExtractsJsonLd_AndRemovesFromText()
    {
        var html = @"
            <html><body>
                <script type=""application/ld+json"">
                    {""@type"": ""Recipe"", ""name"": ""Test Recipe""}
                </script>
                <p>Visible content</p>
            </body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Single(result.JsonLdSnippets);
        Assert.NotNull(result.RecipeJsonLd);
        Assert.Contains("Test Recipe", result.RecipeJsonLd);
        Assert.True(result.HasRecipeJsonLd);
        Assert.Contains("Visible content", result.TextContent);
    }

    [Fact]
    public void Sanitize_NoRecipeJsonLd_HasRecipeJsonLdIsFalse()
    {
        var html = @"
            <html><body>
                <script type=""application/ld+json"">
                    {""@type"": ""Organization"", ""name"": ""Company""}
                </script>
                <p>Content</p>
            </body></html>";
        
        var result = _service.Sanitize(html);

        Assert.Single(result.JsonLdSnippets);
        Assert.Null(result.RecipeJsonLd);
        Assert.False(result.HasRecipeJsonLd);
    }

    [Fact]
    public void Sanitize_CompleteRecipePage_ExtractsAll()
    {
        var html = @"
            <html lang=""en"">
            <head>
                <title>Best Chocolate Cake Recipe</title>
                <meta name=""description"" content=""The best chocolate cake ever"">
                <meta name=""author"" content=""Chef Maria"">
                <meta property=""og:site_name"" content=""Cooking Blog"">
                <link rel=""canonical"" href=""https://example.com/chocolate-cake"">
                <script type=""application/ld+json"">
                {
                    ""@context"": ""https://schema.org"",
                    ""@type"": ""Recipe"",
                    ""name"": ""Chocolate Cake"",
                    ""author"": {""@type"": ""Person"", ""name"": ""Chef Maria""},
                    ""recipeIngredient"": [""1 cup flour"", ""2 eggs"", ""1 cup sugar""],
                    ""recipeInstructions"": [{""@type"": ""HowToStep"", ""text"": ""Mix ingredients""}]
                }
                </script>
            </head>
            <body>
                <nav><a href=""/"">Home</a></nav>
                <article>
                    <h1>Best Chocolate Cake Recipe</h1>
                    <p>This is the best chocolate cake you will ever make.</p>
                    <h2>Ingredients</h2>
                    <ul>
                        <li>1 cup flour</li>
                        <li>2 eggs</li>
                        <li>1 cup sugar</li>
                    </ul>
                    <h2>Instructions</h2>
                    <ol>
                        <li>Mix ingredients</li>
                        <li>Bake at 350 degrees</li>
                    </ol>
                </article>
                <footer>Copyright 2024</footer>
            </body>
            </html>";
        
        var result = _service.Sanitize(html);

        // Check JSON-LD extraction
        Assert.True(result.HasRecipeJsonLd);
        Assert.Contains("Chocolate Cake", result.RecipeJsonLd!);
        Assert.Contains("recipeIngredient", result.RecipeJsonLd);

        // Check metadata
        Assert.Equal("Best Chocolate Cake Recipe", result.Metadata.Title);
        Assert.Equal("The best chocolate cake ever", result.Metadata.Description);
        Assert.Equal("Chef Maria", result.Metadata.Author);
        Assert.Equal("Cooking Blog", result.Metadata.SiteName);
        Assert.Equal("https://example.com/chocolate-cake", result.Metadata.CanonicalUrl);
        Assert.Equal("en", result.Metadata.Language);

        // Check text content
        Assert.Contains("Best Chocolate Cake Recipe", result.TextContent);
        Assert.Contains("Ingredients", result.TextContent);
        Assert.Contains("1 cup flour", result.TextContent);
        Assert.Contains("Instructions", result.TextContent);
        Assert.Contains("Mix ingredients", result.TextContent);
    }

    #endregion
}

/// <summary>
/// Tests for SanitizedContent record.
/// </summary>
public class SanitizedContentTests
{
    [Fact]
    public void HasRecipeJsonLd_WhenPresent_ReturnsTrue()
    {
        var content = new SanitizedContent
        {
            TextContent = "text",
            RecipeJsonLd = "{\"@type\": \"Recipe\"}"
        };

        Assert.True(content.HasRecipeJsonLd);
    }

    [Fact]
    public void HasRecipeJsonLd_WhenNull_ReturnsFalse()
    {
        var content = new SanitizedContent
        {
            TextContent = "text",
            RecipeJsonLd = null
        };

        Assert.False(content.HasRecipeJsonLd);
    }

    [Fact]
    public void HasRecipeJsonLd_WhenEmpty_ReturnsFalse()
    {
        var content = new SanitizedContent
        {
            TextContent = "text",
            RecipeJsonLd = ""
        };

        Assert.False(content.HasRecipeJsonLd);
    }
}

/// <summary>
/// Tests for PageMetadata record.
/// </summary>
public class PageMetadataTests
{
    [Fact]
    public void PageMetadata_DefaultsToNulls()
    {
        var metadata = new PageMetadata();

        Assert.Null(metadata.Title);
        Assert.Null(metadata.Description);
        Assert.Null(metadata.Author);
        Assert.Null(metadata.SiteName);
        Assert.Null(metadata.CanonicalUrl);
        Assert.Null(metadata.Language);
    }
}
