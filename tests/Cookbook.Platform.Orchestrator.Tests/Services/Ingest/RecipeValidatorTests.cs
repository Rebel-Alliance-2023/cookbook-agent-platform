using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for RecipeValidator.
/// </summary>
public class RecipeValidatorTests
{
    private readonly RecipeValidator _validator;

    public RecipeValidatorTests()
    {
        var loggerMock = new Mock<ILogger<RecipeValidator>>();
        _validator = new RecipeValidator(loggerMock.Object);
    }

    #region Valid Recipe Tests

    [Fact]
    public void Validate_ValidRecipe_ReturnsNoErrors()
    {
        var recipe = CreateValidRecipe();

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MinimalValidRecipe_ReturnsNoErrors()
    {
        var recipe = new Recipe
        {
            Id = "test-1",
            Name = "Test Recipe",
            Servings = 4,
            Ingredients = [new Ingredient { Name = "flour", Quantity = 1 }],
            Instructions = ["Mix ingredients"]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ValidRecipe_ReturnsNoErrors()
    {
        var recipe = CreateValidRecipe();

        var result = await _validator.ValidateAsync(recipe);

        Assert.True(result.IsValid);
    }

    #endregion

    #region Required Field Tests

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Name = "" };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("REQUIRED_NAME"));
    }

    [Fact]
    public void Validate_WhitespaceName_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Name = "   " };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("REQUIRED_NAME"));
    }

    [Fact]
    public void Validate_NoIngredients_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Ingredients = [] };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NO_INGREDIENTS"));
    }

    [Fact]
    public void Validate_NoInstructions_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Instructions = [] };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NO_INSTRUCTIONS"));
    }

    #endregion

    #region Field Length Tests

    [Fact]
    public void Validate_NameTooLong_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Name = new string('a', 201) };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NAME_TOO_LONG"));
    }

    [Fact]
    public void Validate_DescriptionTooLong_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Description = new string('a', 5001) };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("DESCRIPTION_TOO_LONG"));
    }

    [Fact]
    public void Validate_InstructionTooLong_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Instructions = [new string('a', 2001)]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INSTRUCTION_TOO_LONG"));
    }

    [Fact]
    public void Validate_IngredientNameTooLong_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients = [new Ingredient { Name = new string('a', 201), Quantity = 1 }]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INGREDIENT_NAME_TOO_LONG"));
    }

    [Fact]
    public void Validate_TagTooLong_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Tags = [new string('a', 51)]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TAG_TOO_LONG"));
    }

    #endregion

    #region Numeric Constraint Tests

    [Fact]
    public void Validate_NegativePrepTime_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { PrepTimeMinutes = -1 };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NEGATIVE_PREP_TIME"));
    }

    [Fact]
    public void Validate_NegativeCookTime_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { CookTimeMinutes = -1 };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NEGATIVE_COOK_TIME"));
    }

    [Fact]
    public void Validate_ZeroServings_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Servings = 0 };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INVALID_SERVINGS"));
    }

    [Fact]
    public void Validate_NegativeServings_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { Servings = -4 };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INVALID_SERVINGS"));
    }

    [Fact]
    public void Validate_NegativeIngredientQuantity_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients = [new Ingredient { Name = "flour", Quantity = -1 }]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NEGATIVE_INGREDIENT_QUANTITY"));
    }

    #endregion

    #region Count Limit Tests

    [Fact]
    public void Validate_TooManyIngredients_ReturnsError()
    {
        var ingredients = Enumerable.Range(1, 101)
            .Select(i => new Ingredient { Name = $"Ingredient {i}", Quantity = 1 })
            .ToList();
        var recipe = CreateValidRecipe() with { Ingredients = ingredients };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TOO_MANY_INGREDIENTS"));
    }

    [Fact]
    public void Validate_TooManyInstructions_ReturnsError()
    {
        var instructions = Enumerable.Range(1, 101)
            .Select(i => $"Step {i}")
            .ToList();
        var recipe = CreateValidRecipe() with { Instructions = instructions };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TOO_MANY_INSTRUCTIONS"));
    }

    [Fact]
    public void Validate_TooManyTags_ReturnsError()
    {
        var tags = Enumerable.Range(1, 21)
            .Select(i => $"Tag{i}")
            .ToList();
        var recipe = CreateValidRecipe() with { Tags = tags };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TOO_MANY_TAGS"));
    }

    #endregion

    #region Ingredient Validation Tests

    [Fact]
    public void Validate_IngredientWithEmptyName_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients = [new Ingredient { Name = "", Quantity = 1 }]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INGREDIENT_NO_NAME"));
    }

    [Fact]
    public void Validate_IngredientWithWhitespaceName_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients = [new Ingredient { Name = "   ", Quantity = 1 }]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INGREDIENT_NO_NAME"));
    }

    #endregion

    #region Instruction Validation Tests

    [Fact]
    public void Validate_EmptyInstruction_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Instructions = ["Step 1", "", "Step 3"]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("EMPTY_INSTRUCTION"));
    }

    [Fact]
    public void Validate_WhitespaceInstruction_ReturnsError()
    {
        var recipe = CreateValidRecipe() with
        {
            Instructions = ["Step 1", "   "]
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("EMPTY_INSTRUCTION"));
    }

    #endregion

    #region URL Validation Tests

    [Fact]
    public void Validate_InvalidImageUrl_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { ImageUrl = "not-a-url" };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INVALID_IMAGE_URL"));
    }

    [Fact]
    public void Validate_NonHttpImageUrl_ReturnsError()
    {
        var recipe = CreateValidRecipe() with { ImageUrl = "ftp://example.com/image.jpg" };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("INVALID_IMAGE_URL"));
    }

    [Fact]
    public void Validate_ValidHttpsImageUrl_NoError()
    {
        var recipe = CreateValidRecipe() with { ImageUrl = "https://example.com/image.jpg" };

        var result = _validator.Validate(recipe);

        Assert.DoesNotContain(result.Errors, e => e.Contains("INVALID_IMAGE_URL"));
    }

    #endregion

    #region Business Rule Warning Tests

    [Fact]
    public void Validate_LongPrepTime_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { PrepTimeMinutes = 25 * 60 }; // 25 hours

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid); // Still valid
        Assert.Contains(result.Warnings, w => w.Contains("LONG_PREP_TIME"));
    }

    [Fact]
    public void Validate_LongCookTime_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { CookTimeMinutes = 73 * 60 }; // 73 hours

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("LONG_COOK_TIME"));
    }

    [Fact]
    public void Validate_ZeroTotalTime_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { PrepTimeMinutes = 0, CookTimeMinutes = 0 };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("NO_TIME_ESTIMATES"));
    }

    [Fact]
    public void Validate_HighServings_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { Servings = 150 };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("HIGH_SERVINGS"));
    }

    [Fact]
    public void Validate_MissingDescriptionWithManyIngredients_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with
        {
            Description = null,
            Ingredients =
            [
                new Ingredient { Name = "flour", Quantity = 1 },
                new Ingredient { Name = "sugar", Quantity = 1 },
                new Ingredient { Name = "butter", Quantity = 1 }
            ]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("MISSING_DESCRIPTION"));
    }

    [Fact]
    public void Validate_ShortDescription_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { Description = "Short" };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("SHORT_DESCRIPTION"));
    }

    [Fact]
    public void Validate_MissingCuisine_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { Cuisine = null };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("MISSING_CUISINE"));
    }

    [Fact]
    public void Validate_NoTags_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { Tags = [] };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("NO_TAGS"));
    }

    [Fact]
    public void Validate_NoImage_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with { ImageUrl = null };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("NO_IMAGE"));
    }

    [Fact]
    public void Validate_FewIngredientsWithManyInstructions_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients = [new Ingredient { Name = "flour", Quantity = 1 }],
            Instructions = ["Step 1", "Step 2", "Step 3", "Step 4", "Step 5", "Step 6"]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("FEW_INGREDIENTS"));
    }

    [Fact]
    public void Validate_FewInstructionsWithManyIngredients_ReturnsWarning()
    {
        var ingredients = Enumerable.Range(1, 6)
            .Select(i => new Ingredient { Name = $"Ingredient {i}", Quantity = 1 })
            .ToList();
        var recipe = CreateValidRecipe() with
        {
            Ingredients = ingredients,
            Instructions = ["Mix everything"]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("FEW_INSTRUCTIONS"));
    }

    [Fact]
    public void Validate_DuplicateIngredients_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients =
            [
                new Ingredient { Name = "flour", Quantity = 2, Unit = "cups" },
                new Ingredient { Name = "Flour", Quantity = 1, Unit = "cup" }, // Duplicate (case-insensitive)
                new Ingredient { Name = "sugar", Quantity = 1, Unit = "cup" }
            ]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("DUPLICATE_INGREDIENTS"));
    }

    [Fact]
    public void Validate_ZeroQuantityIngredients_ReturnsWarning()
    {
        var recipe = CreateValidRecipe() with
        {
            Ingredients =
            [
                new Ingredient { Name = "flour", Quantity = 0 },
                new Ingredient { Name = "sugar", Quantity = 1 }
            ]
        };

        var result = _validator.Validate(recipe);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ZERO_QUANTITY_INGREDIENTS"));
    }

    #endregion

    #region Multiple Error Tests

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        var recipe = new Recipe
        {
            Id = "test",
            Name = "", // Error
            Servings = -1, // Error
            Ingredients = [], // Error
            Instructions = [] // Error
        };

        var result = _validator.Validate(recipe);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 4);
    }

    #endregion

    #region ValidationIssue Tests

    [Fact]
    public void ValidationIssue_HasRequiredProperties()
    {
        var issue = new ValidationIssue
        {
            Field = "Name",
            Message = "Name is required",
            Severity = ValidationSeverity.Error,
            Code = "REQUIRED_NAME"
        };

        Assert.Equal("Name", issue.Field);
        Assert.Equal("Name is required", issue.Message);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("REQUIRED_NAME", issue.Code);
    }

    #endregion

    #region Helper Methods

    private static Recipe CreateValidRecipe()
    {
        return new Recipe
        {
            Id = "test-recipe-1",
            Name = "Chocolate Chip Cookies",
            Description = "Delicious homemade chocolate chip cookies that are crispy on the outside and chewy on the inside.",
            PrepTimeMinutes = 15,
            CookTimeMinutes = 12,
            Servings = 24,
            Cuisine = "American",
            DietType = "Vegetarian",
            ImageUrl = "https://example.com/cookies.jpg",
            Tags = ["dessert", "cookies", "baking"],
            Ingredients =
            [
                new Ingredient { Name = "all-purpose flour", Quantity = 2.25, Unit = "cups" },
                new Ingredient { Name = "butter", Quantity = 1, Unit = "cup" },
                new Ingredient { Name = "sugar", Quantity = 0.75, Unit = "cup" },
                new Ingredient { Name = "chocolate chips", Quantity = 2, Unit = "cups" }
            ],
            Instructions =
            [
                "Preheat oven to 375°F (190°C).",
                "Cream together butter and sugar until fluffy.",
                "Add flour gradually and mix until combined.",
                "Fold in chocolate chips.",
                "Drop rounded tablespoons onto baking sheets.",
                "Bake for 9-11 minutes or until golden brown."
            ]
        };
    }

    #endregion
}

/// <summary>
/// Tests for ValidationReport record.
/// </summary>
public class ValidationReportTests
{
    [Fact]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        var report = new ValidationReport
        {
            Errors = [],
            Warnings = ["Some warning"]
        };

        Assert.True(report.IsValid);
    }

    [Fact]
    public void IsValid_HasErrors_ReturnsFalse()
    {
        var report = new ValidationReport
        {
            Errors = ["Some error"],
            Warnings = []
        };

        Assert.False(report.IsValid);
    }
}
