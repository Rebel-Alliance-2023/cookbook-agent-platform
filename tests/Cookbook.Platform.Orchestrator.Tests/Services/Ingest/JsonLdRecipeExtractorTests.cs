using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for JsonLdRecipeExtractor.
/// </summary>
public class JsonLdRecipeExtractorTests
{
    private readonly JsonLdRecipeExtractor _extractor;

    public JsonLdRecipeExtractorTests()
    {
        var loggerMock = new Mock<ILogger<JsonLdRecipeExtractor>>();
        _extractor = new JsonLdRecipeExtractor(loggerMock.Object);
    }

    #region CanExtract Tests

    [Theory]
    [InlineData("{}", true)]
    [InlineData("[]", true)]
    [InlineData("{\"@type\": \"Recipe\"}", true)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void CanExtract_ReturnsCorrectValue(string? content, bool expected)
    {
        var result = _extractor.CanExtract(content!);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Basic Extraction Tests

    [Fact]
    public async Task ExtractAsync_EmptyContent_ReturnsFailure()
    {
        var result = await _extractor.ExtractAsync("", new ExtractionContext());

        Assert.False(result.Success);
        Assert.Equal("EMPTY_CONTENT", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_InvalidJson_ReturnsFailure()
    {
        var result = await _extractor.ExtractAsync("not valid json", new ExtractionContext());

        Assert.False(result.Success);
        Assert.Equal("INVALID_JSON", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_MissingName_ReturnsFailure()
    {
        var json = """{"@type": "Recipe", "description": "A tasty dish"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.False(result.Success);
        Assert.Equal("MAPPING_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task ExtractAsync_MinimalRecipe_ReturnsSuccess()
    {
        var json = """{"@type": "Recipe", "name": "Test Recipe"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.NotNull(result.Recipe);
        Assert.Equal("Test Recipe", result.Recipe.Name);
        Assert.Equal(ExtractionMethod.JsonLd, result.Method);
    }

    [Fact]
    public async Task ExtractAsync_FullRecipe_ExtractsAllFields()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Chocolate Chip Cookies",
                "description": "Classic cookies with chocolate chips",
                "prepTime": "PT20M",
                "cookTime": "PT12M",
                "recipeYield": "24 cookies",
                "recipeCuisine": "American",
                "recipeCategory": "Dessert",
                "keywords": "cookies, chocolate, baking",
                "image": "https://example.com/cookies.jpg",
                "recipeIngredient": [
                    "2 cups flour",
                    "1 cup sugar",
                    "1/2 cup butter"
                ],
                "recipeInstructions": [
                    {"@type": "HowToStep", "text": "Mix dry ingredients"},
                    {"@type": "HowToStep", "text": "Add butter and mix"},
                    {"@type": "HowToStep", "text": "Bake at 350F"}
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        var recipe = result.Recipe!;
        
        Assert.Equal("Chocolate Chip Cookies", recipe.Name);
        Assert.Equal("Classic cookies with chocolate chips", recipe.Description);
        Assert.Equal(20, recipe.PrepTimeMinutes);
        Assert.Equal(12, recipe.CookTimeMinutes);
        Assert.Equal(24, recipe.Servings);
        Assert.Equal("American", recipe.Cuisine);
        Assert.Equal("https://example.com/cookies.jpg", recipe.ImageUrl);
        Assert.Equal(3, recipe.Ingredients.Count);
        Assert.Equal(3, recipe.Instructions.Count);
        Assert.Contains("Dessert", recipe.Tags);
        Assert.Contains("cookies", recipe.Tags);
    }

    #endregion

    #region Duration Parsing Tests

    [Theory]
    [InlineData("PT1H", 60)]
    [InlineData("PT30M", 30)]
    [InlineData("PT1H30M", 90)]
    [InlineData("PT2H15M", 135)]
    [InlineData("PT45M", 45)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("invalid", 0)]
    public async Task ExtractAsync_ParsesDuration_Correctly(string? duration, int expectedMinutes)
    {
        var json = $$"""{"@type": "Recipe", "name": "Test", "prepTime": "{{duration ?? ""}}"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(expectedMinutes, result.Recipe!.PrepTimeMinutes);
    }

    #endregion

    #region Ingredient Parsing Tests

    [Fact]
    public async Task ExtractAsync_ParsesIngredients_WithQuantityAndUnit()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeIngredient": [
                    "2 cups flour",
                    "1 tablespoon sugar",
                    "1/2 teaspoon salt"
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(3, result.Recipe!.Ingredients.Count);

        var flour = result.Recipe.Ingredients[0];
        Assert.Equal("flour", flour.Name);
        Assert.Equal(2, flour.Quantity);
        Assert.Equal("cups", flour.Unit);

        var sugar = result.Recipe.Ingredients[1];
        Assert.Equal("sugar", sugar.Name);
        Assert.Equal(1, sugar.Quantity);
        Assert.Equal("tablespoon", sugar.Unit);

        var salt = result.Recipe.Ingredients[2];
        Assert.Equal("salt", salt.Name);
        Assert.Equal(0.5, salt.Quantity);
        Assert.Equal("teaspoon", salt.Unit);
    }

    [Fact]
    public async Task ExtractAsync_ParsesIngredients_WithFractions()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeIngredient": [
                    "1 1/2 cups milk",
                    "¼ cup honey",
                    "½ teaspoon vanilla"
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        
        Assert.Equal(1.5, result.Recipe!.Ingredients[0].Quantity);
        Assert.Equal(0.25, result.Recipe.Ingredients[1].Quantity);
        Assert.Equal(0.5, result.Recipe.Ingredients[2].Quantity);
    }

    [Fact]
    public async Task ExtractAsync_ParsesIngredients_WithNotes()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeIngredient": [
                    "2 eggs (room temperature)",
                    "1 cup chicken broth (low sodium)"
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal("room temperature", result.Recipe!.Ingredients[0].Notes);
        Assert.Equal("low sodium", result.Recipe.Ingredients[1].Notes);
    }

    #endregion

    #region Instructions Parsing Tests

    [Fact]
    public async Task ExtractAsync_ParsesInstructions_HowToSteps()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeInstructions": [
                    {"@type": "HowToStep", "text": "Step 1"},
                    {"@type": "HowToStep", "text": "Step 2"}
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(2, result.Recipe!.Instructions.Count);
        Assert.Equal("Step 1", result.Recipe.Instructions[0]);
        Assert.Equal("Step 2", result.Recipe.Instructions[1]);
    }

    [Fact]
    public async Task ExtractAsync_ParsesInstructions_StringArray()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeInstructions": [
                    "First step",
                    "Second step"
                ]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(2, result.Recipe!.Instructions.Count);
    }

    [Fact]
    public async Task ExtractAsync_ParsesInstructions_SingleString()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "recipeInstructions": "Mix all ingredients.\nBake at 350F.\nServe warm."
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.True(result.Recipe!.Instructions.Count >= 2);
    }

    #endregion

    #region Servings Parsing Tests

    [Theory]
    [InlineData("4", 4)]
    [InlineData("6 servings", 6)]
    [InlineData("Makes 12", 12)]
    [InlineData("Serves 8", 8)]
    public async Task ExtractAsync_ParsesServings_FromString(string yieldStr, int expected)
    {
        var json = $$"""{"@type": "Recipe", "name": "Test", "recipeYield": "{{yieldStr}}"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(expected, result.Recipe!.Servings);
    }

    [Fact]
    public async Task ExtractAsync_ParsesServings_FromNumber()
    {
        var json = """{"@type": "Recipe", "name": "Test", "recipeYield": 6}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(6, result.Recipe!.Servings);
    }

    #endregion

    #region Nutrition Parsing Tests

    [Fact]
    public async Task ExtractAsync_ParsesNutrition()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "nutrition": {
                    "@type": "NutritionInformation",
                    "calories": "250 kcal",
                    "proteinContent": "10g",
                    "carbohydrateContent": "30 grams",
                    "fatContent": 8,
                    "fiberContent": "5g"
                }
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.NotNull(result.Recipe!.Nutrition);
        Assert.Equal(250, result.Recipe.Nutrition.Calories);
        Assert.Equal(10, result.Recipe.Nutrition.ProteinGrams);
        Assert.Equal(30, result.Recipe.Nutrition.CarbsGrams);
        Assert.Equal(8, result.Recipe.Nutrition.FatGrams);
        Assert.Equal(5, result.Recipe.Nutrition.FiberGrams);
    }

    #endregion

    #region Image URL Parsing Tests

    [Fact]
    public async Task ExtractAsync_ParsesImage_String()
    {
        var json = """{"@type": "Recipe", "name": "Test", "image": "https://example.com/image.jpg"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/image.jpg", result.Recipe!.ImageUrl);
    }

    [Fact]
    public async Task ExtractAsync_ParsesImage_Array()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "image": ["https://example.com/image1.jpg", "https://example.com/image2.jpg"]
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/image1.jpg", result.Recipe!.ImageUrl);
    }

    [Fact]
    public async Task ExtractAsync_ParsesImage_Object()
    {
        var json = """
            {
                "@type": "Recipe",
                "name": "Test",
                "image": {"@type": "ImageObject", "url": "https://example.com/image.jpg"}
            }
            """;

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal("https://example.com/image.jpg", result.Recipe!.ImageUrl);
    }

    #endregion

    #region ExtractionResult Tests

    [Fact]
    public async Task ExtractAsync_SetsRawSource()
    {
        var json = """{"@type": "Recipe", "name": "Test"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(json, result.RawSource);
    }

    [Fact]
    public async Task ExtractAsync_SetsHighConfidence()
    {
        var json = """{"@type": "Recipe", "name": "Test"}""";

        var result = await _extractor.ExtractAsync(json, new ExtractionContext());

        Assert.True(result.Success);
        Assert.Equal(0.95, result.Confidence);
    }

    #endregion
}

/// <summary>
/// Unit tests for ExtractionResult.
/// </summary>
public class ExtractionResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessResult()
    {
        var recipe = new Recipe { Id = "test", Name = "Test Recipe" };
        
        var result = ExtractionResult.Succeeded(recipe, ExtractionMethod.JsonLd, 0.9);

        Assert.True(result.Success);
        Assert.Same(recipe, result.Recipe);
        Assert.Equal(ExtractionMethod.JsonLd, result.Method);
        Assert.Equal(0.9, result.Confidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failed_CreatesFailureResult()
    {
        var result = ExtractionResult.Failed("Parse error", "PARSE_ERROR", ExtractionMethod.Llm);

        Assert.False(result.Success);
        Assert.Null(result.Recipe);
        Assert.Equal("Parse error", result.Error);
        Assert.Equal("PARSE_ERROR", result.ErrorCode);
        Assert.Equal(ExtractionMethod.Llm, result.Method);
    }
}

/// <summary>
/// Unit tests for ExtractionContext.
/// </summary>
public class ExtractionContextTests
{
    [Fact]
    public void ExtractionContext_HasDefaultContentBudget()
    {
        var context = new ExtractionContext();

        Assert.Equal(60_000, context.ContentBudget);
    }
}
