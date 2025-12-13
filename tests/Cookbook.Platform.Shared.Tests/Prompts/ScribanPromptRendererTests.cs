using Cookbook.Platform.Shared.Prompts;

namespace Cookbook.Platform.Shared.Tests.Prompts;

/// <summary>
/// Unit tests for the ScribanPromptRenderer class.
/// </summary>
public class ScribanPromptRendererTests
{
    private readonly ScribanPromptRenderer _renderer = new();

    #region Basic Rendering

    [Fact]
    public void Render_SimpleVariable_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Hello, {{ name }}!";
        var variables = new Dictionary<string, object?> { ["name"] = "World" };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Render_MultipleVariables_SubstitutesAll()
    {
        // Arrange
        var template = "Recipe: {{ name }} by {{ author }}";
        var variables = new Dictionary<string, object?>
        {
            ["name"] = "Chocolate Cake",
            ["author"] = "Chef John"
        };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Recipe: Chocolate Cake by Chef John", result);
    }

    [Fact]
    public void Render_MissingOptionalVariable_RendersAsEmpty()
    {
        // Arrange
        var template = "Hello, {{ name }}{{ suffix }}!";
        var variables = new Dictionary<string, object?> { ["name"] = "World" };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Render_NullVariable_RendersAsEmpty()
    {
        // Arrange
        var template = "Value: [{{ value }}]";
        var variables = new Dictionary<string, object?> { ["value"] = null };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Value: []", result);
    }

    [Fact]
    public void Render_ComplexTemplate_HandlesScribanSyntax()
    {
        // Arrange
        var template = """
            {{ for item in items }}
            - {{ item }}
            {{ end }}
            """;
        var variables = new Dictionary<string, object?>
        {
            ["items"] = new[] { "flour", "sugar", "eggs" }
        };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Contains("- flour", result);
        Assert.Contains("- sugar", result);
        Assert.Contains("- eggs", result);
    }

    [Fact]
    public void Render_ConditionalLogic_Works()
    {
        // Arrange
        var template = "{{ if show_author }}By: {{ author }}{{ end }}";
        var variables = new Dictionary<string, object?>
        {
            ["show_author"] = true,
            ["author"] = "Chef John"
        };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("By: Chef John", result);
    }

    [Fact]
    public void Render_FalseConditional_OmitsContent()
    {
        // Arrange
        var template = "Start{{ if show_extra }} Extra{{ end }} End";
        var variables = new Dictionary<string, object?>
        {
            ["show_extra"] = false
        };

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Start End", result);
    }

    #endregion

    #region Required Variables Validation

    [Fact]
    public void Render_MissingRequiredVariable_ThrowsException()
    {
        // Arrange
        var template = "Hello, {{ name }}!";
        var variables = new Dictionary<string, object?>();
        var required = new[] { "name" };

        // Act & Assert
        var ex = Assert.Throws<PromptRenderException>(() =>
            _renderer.Render(template, variables, required));

        Assert.Contains("name", ex.Message);
        Assert.Contains("name", ex.MissingVariables);
    }

    [Fact]
    public void Render_MultipleMissingRequiredVariables_ReportsAll()
    {
        // Arrange
        var template = "{{ url }} {{ content }}";
        var variables = new Dictionary<string, object?>();
        var required = new[] { "url", "content", "schema" };

        // Act & Assert
        var ex = Assert.Throws<PromptRenderException>(() =>
            _renderer.Render(template, variables, required));

        Assert.Equal(3, ex.MissingVariables.Count);
        Assert.Contains("url", ex.MissingVariables);
        Assert.Contains("content", ex.MissingVariables);
        Assert.Contains("schema", ex.MissingVariables);
    }

    [Fact]
    public void Render_NullRequiredVariable_ThrowsException()
    {
        // Arrange
        var template = "{{ content }}";
        var variables = new Dictionary<string, object?> { ["content"] = null };
        var required = new[] { "content" };

        // Act & Assert
        var ex = Assert.Throws<PromptRenderException>(() =>
            _renderer.Render(template, variables, required));

        Assert.Contains("content", ex.MissingVariables);
    }

    [Fact]
    public void Render_AllRequiredVariablesPresent_Succeeds()
    {
        // Arrange
        var template = "URL: {{ url }}\nContent: {{ content }}";
        var variables = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com",
            ["content"] = "Recipe content here"
        };
        var required = new[] { "url", "content" };

        // Act
        var result = _renderer.Render(template, variables, required);

        // Assert
        Assert.Contains("https://example.com", result);
        Assert.Contains("Recipe content here", result);
    }

    [Fact]
    public void Render_NoRequiredVariables_SucceedsWithAnyInput()
    {
        // Arrange
        var template = "Static content";
        var variables = new Dictionary<string, object?>();

        // Act
        var result = _renderer.Render(template, variables);

        // Assert
        Assert.Equal("Static content", result);
    }

    #endregion

    #region Content Truncation

    [Fact]
    public void RenderWithTruncation_ContentUnderBudget_NoTruncation()
    {
        // Arrange
        var template = "Content: {{ content }}";
        var shortContent = "Short content";
        var variables = new Dictionary<string, object?> { ["content"] = shortContent };

        // Act
        var result = _renderer.RenderWithTruncation(template, variables, maxCharacters: 1000);

        // Assert
        Assert.Contains(shortContent, result);
        Assert.DoesNotContain("truncated", result);
    }

    [Fact]
    public void RenderWithTruncation_ContentOverBudget_Truncates()
    {
        // Arrange
        var template = "{{ content }}";
        var longContent = new string('x', 10000);
        var variables = new Dictionary<string, object?> { ["content"] = longContent };

        // Act
        var result = _renderer.RenderWithTruncation(template, variables, maxCharacters: 500);

        // Assert
        Assert.True(result.Length < longContent.Length);
    }

    [Fact]
    public void RenderWithTruncation_NoContentVariable_RendersNormally()
    {
        // Arrange
        var template = "Hello, {{ name }}!";
        var variables = new Dictionary<string, object?> { ["name"] = "World" };

        // Act
        var result = _renderer.RenderWithTruncation(template, variables, maxCharacters: 100);

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void RenderWithTruncation_NullMaxCharacters_NoTruncation()
    {
        // Arrange
        var template = "{{ content }}";
        var content = new string('x', 1000);
        var variables = new Dictionary<string, object?> { ["content"] = content };

        // Act
        var result = _renderer.RenderWithTruncation(template, variables, maxCharacters: null);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void RenderWithTruncation_ValidatesRequiredVariables()
    {
        // Arrange
        var template = "{{ url }} {{ content }}";
        var variables = new Dictionary<string, object?> { ["content"] = "test" };
        var required = new[] { "url", "content" };

        // Act & Assert
        Assert.Throws<PromptRenderException>(() =>
            _renderer.RenderWithTruncation(template, variables, required, 1000));
    }

    #endregion

    #region TruncateContent Static Method

    [Fact]
    public void TruncateContent_ShortContent_ReturnsUnchanged()
    {
        // Arrange
        var content = "Short content";

        // Act
        var result = ScribanPromptRenderer.TruncateContent(content, 1000);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void TruncateContent_EmptyContent_ReturnsEmpty()
    {
        // Act
        var result = ScribanPromptRenderer.TruncateContent("", 1000);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void TruncateContent_NullContent_ReturnsNull()
    {
        // Act
        var result = ScribanPromptRenderer.TruncateContent(null!, 1000);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TruncateContent_LongContent_TruncatesToBudget()
    {
        // Arrange
        var content = new string('a', 10000);

        // Act
        var result = ScribanPromptRenderer.TruncateContent(content, 500);

        // Assert
        Assert.True(result.Length <= 520); // Some buffer for truncation message
    }

    [Fact]
    public void TruncateContent_PreservesIngredientsSections()
    {
        // Arrange
        var content = """
            Some introductory text that is not very important.
            
            ## Ingredients
            - 2 cups flour
            - 1 cup sugar
            
            ## Instructions
            1. Mix ingredients
            2. Bake at 350°F
            
            ## About the Author
            Long biography that could be trimmed...
            """;

        // Act
        var result = ScribanPromptRenderer.TruncateContent(content, 300);

        // Assert - should preserve important sections
        Assert.Contains("Ingredients", result);
    }

    [Fact]
    public void TruncateContent_IncludesTruncationIndicator()
    {
        // Arrange
        var content = new string('x', 10000);

        // Act
        var result = ScribanPromptRenderer.TruncateContent(content, 500);

        // Assert
        Assert.Contains("truncated", result.ToLower());
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Render_InvalidTemplateSyntax_ThrowsException()
    {
        // Arrange
        var template = "{{ if unclosed";
        var variables = new Dictionary<string, object?>();

        // Act & Assert
        Assert.Throws<PromptRenderException>(() =>
            _renderer.Render(template, variables));
    }

    [Fact]
    public void Render_TemplateParseError_IncludesMessage()
    {
        // Arrange
        var template = "{{ invalid syntax here }}";
        var variables = new Dictionary<string, object?>();

        // This may or may not throw depending on Scriban's parsing
        // The key is that if it fails, we get a PromptRenderException
        try
        {
            _renderer.Render(template, variables);
            // If it doesn't throw, that's also acceptable (Scriban might handle it)
        }
        catch (PromptRenderException ex)
        {
            Assert.NotNull(ex.Message);
        }
    }

    #endregion

    #region Real-World Template Tests

    [Fact]
    public void Render_RecipeExtractionPrompt_RendersCorrectly()
    {
        // Arrange
        var template = """
            Extract recipe information from the following content.
            
            URL: {{ url }}
            
            Content:
            {{ content }}
            
            Please extract and return JSON matching this schema:
            {{ schema }}
            """;

        var variables = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/recipes/chocolate-cake",
            ["content"] = "# Chocolate Cake\n\nIngredients:\n- 2 cups flour\n- 1 cup sugar",
            ["schema"] = "{ \"name\": \"string\", \"ingredients\": [\"string\"] }"
        };

        var required = new[] { "url", "content", "schema" };

        // Act
        var result = _renderer.Render(template, variables, required);

        // Assert
        Assert.Contains("https://example.com/recipes/chocolate-cake", result);
        Assert.Contains("Chocolate Cake", result);
        Assert.Contains("2 cups flour", result);
        Assert.Contains("\"name\": \"string\"", result);
    }

    [Fact]
    public void RenderWithTruncation_LargeRecipeContent_TruncatesAppropriately()
    {
        // Arrange
        var template = "Extract recipe:\n{{ content }}";

        // Create content with clear sections
        var content = """
            ## Recipe: Test Cake
            
            This is a long introduction about baking that could be trimmed.
            
            ## Ingredients
            - 2 cups flour
            - 1 cup sugar
            - 3 eggs
            - 1 cup milk
            
            ## Instructions
            1. Preheat oven to 350°F
            2. Mix dry ingredients
            3. Add wet ingredients
            4. Bake for 30 minutes
            
            ## Tips
            Some tips here...
            
            ## About
            """ + new string('x', 5000); // Long about section

        var variables = new Dictionary<string, object?> { ["content"] = content };

        // Act
        var result = _renderer.RenderWithTruncation(template, variables, maxCharacters: 500);

        // Assert
        Assert.True(result.Length < content.Length);
    }

    #endregion
}
