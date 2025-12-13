using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Implementation of IRecipeValidator with schema and business validation rules.
/// </summary>
public class RecipeValidator : IRecipeValidator
{
    private readonly ILogger<RecipeValidator> _logger;

    // Schema validation constants
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 5000;
    private const int MaxIngredientNameLength = 200;
    private const int MaxInstructionLength = 2000;
    private const int MaxTagLength = 50;
    private const int MaxTagCount = 20;
    private const int MaxIngredientsCount = 100;
    private const int MaxInstructionsCount = 100;

    // Business validation constants
    private const int MaxReasonablePrepTimeMinutes = 24 * 60; // 24 hours
    private const int MaxReasonableCookTimeMinutes = 72 * 60; // 72 hours
    private const int MaxReasonableServings = 100;
    private const int MinReasonableServings = 1;
    private const int WarnIfNoDescriptionMinIngredients = 3;

    public RecipeValidator(ILogger<RecipeValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a recipe synchronously.
    /// </summary>
    public ValidationReport Validate(Recipe recipe)
    {
        var issues = new List<ValidationIssue>();

        // Schema validation (errors)
        ValidateSchema(recipe, issues);

        // Business validation (mostly warnings)
        ValidateBusinessRules(recipe, issues);

        // Build the report
        var errors = issues
            .Where(i => i.Severity == ValidationSeverity.Error)
            .Select(i => FormatIssue(i))
            .ToList();

        var warnings = issues
            .Where(i => i.Severity == ValidationSeverity.Warning)
            .Select(i => FormatIssue(i))
            .ToList();

        _logger.LogDebug("Validated recipe '{Name}': {Errors} errors, {Warnings} warnings",
            recipe.Name, errors.Count, warnings.Count);

        return new ValidationReport
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Validates a recipe asynchronously.
    /// </summary>
    public Task<ValidationReport> ValidateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        // Currently no async validation rules, but interface allows for future expansion
        // (e.g., checking for duplicate recipes in database, external API validation)
        return Task.FromResult(Validate(recipe));
    }

    #region Schema Validation

    /// <summary>
    /// Validates schema/structural requirements (produces errors).
    /// </summary>
    private void ValidateSchema(Recipe recipe, List<ValidationIssue> issues)
    {
        // Required field: Name
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            issues.Add(new ValidationIssue
            {
                Field = "Name",
                Message = "Recipe name is required",
                Severity = ValidationSeverity.Error,
                Code = "REQUIRED_NAME"
            });
        }
        else if (recipe.Name.Length > MaxNameLength)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Name",
                Message = $"Recipe name exceeds maximum length of {MaxNameLength} characters",
                Severity = ValidationSeverity.Error,
                Code = "NAME_TOO_LONG"
            });
        }

        // Description length
        if (recipe.Description?.Length > MaxDescriptionLength)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Description",
                Message = $"Description exceeds maximum length of {MaxDescriptionLength} characters",
                Severity = ValidationSeverity.Error,
                Code = "DESCRIPTION_TOO_LONG"
            });
        }

        // Prep time must be non-negative
        if (recipe.PrepTimeMinutes < 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "PrepTimeMinutes",
                Message = "Prep time cannot be negative",
                Severity = ValidationSeverity.Error,
                Code = "NEGATIVE_PREP_TIME"
            });
        }

        // Cook time must be non-negative
        if (recipe.CookTimeMinutes < 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "CookTimeMinutes",
                Message = "Cook time cannot be negative",
                Severity = ValidationSeverity.Error,
                Code = "NEGATIVE_COOK_TIME"
            });
        }

        // Servings must be positive
        if (recipe.Servings <= 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Servings",
                Message = "Servings must be a positive number",
                Severity = ValidationSeverity.Error,
                Code = "INVALID_SERVINGS"
            });
        }

        // Ingredients validation
        if (recipe.Ingredients.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Ingredients",
                Message = "Recipe must have at least one ingredient",
                Severity = ValidationSeverity.Error,
                Code = "NO_INGREDIENTS"
            });
        }
        else if (recipe.Ingredients.Count > MaxIngredientsCount)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Ingredients",
                Message = $"Recipe has too many ingredients (max {MaxIngredientsCount})",
                Severity = ValidationSeverity.Error,
                Code = "TOO_MANY_INGREDIENTS"
            });
        }

        // Validate each ingredient
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            var ingredient = recipe.Ingredients[i];
            
            if (string.IsNullOrWhiteSpace(ingredient.Name))
            {
                issues.Add(new ValidationIssue
                {
                    Field = $"Ingredients[{i}].Name",
                    Message = $"Ingredient at position {i + 1} has no name",
                    Severity = ValidationSeverity.Error,
                    Code = "INGREDIENT_NO_NAME"
                });
            }
            else if (ingredient.Name.Length > MaxIngredientNameLength)
            {
                issues.Add(new ValidationIssue
                {
                    Field = $"Ingredients[{i}].Name",
                    Message = $"Ingredient name at position {i + 1} is too long (max {MaxIngredientNameLength})",
                    Severity = ValidationSeverity.Error,
                    Code = "INGREDIENT_NAME_TOO_LONG"
                });
            }

            if (ingredient.Quantity < 0)
            {
                issues.Add(new ValidationIssue
                {
                    Field = $"Ingredients[{i}].Quantity",
                    Message = $"Ingredient quantity at position {i + 1} cannot be negative",
                    Severity = ValidationSeverity.Error,
                    Code = "NEGATIVE_INGREDIENT_QUANTITY"
                });
            }
        }

        // Instructions validation
        if (recipe.Instructions.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Instructions",
                Message = "Recipe must have at least one instruction",
                Severity = ValidationSeverity.Error,
                Code = "NO_INSTRUCTIONS"
            });
        }
        else if (recipe.Instructions.Count > MaxInstructionsCount)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Instructions",
                Message = $"Recipe has too many instructions (max {MaxInstructionsCount})",
                Severity = ValidationSeverity.Error,
                Code = "TOO_MANY_INSTRUCTIONS"
            });
        }

        // Validate each instruction
        for (int i = 0; i < recipe.Instructions.Count; i++)
        {
            var instruction = recipe.Instructions[i];
            
            if (string.IsNullOrWhiteSpace(instruction))
            {
                issues.Add(new ValidationIssue
                {
                    Field = $"Instructions[{i}]",
                    Message = $"Instruction at step {i + 1} is empty",
                    Severity = ValidationSeverity.Error,
                    Code = "EMPTY_INSTRUCTION"
                });
            }
            else if (instruction.Length > MaxInstructionLength)
            {
                issues.Add(new ValidationIssue
                {
                    Field = $"Instructions[{i}]",
                    Message = $"Instruction at step {i + 1} is too long (max {MaxInstructionLength})",
                    Severity = ValidationSeverity.Error,
                    Code = "INSTRUCTION_TOO_LONG"
                });
            }
        }

        // Tags validation
        if (recipe.Tags.Count > MaxTagCount)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Tags",
                Message = $"Recipe has too many tags (max {MaxTagCount})",
                Severity = ValidationSeverity.Error,
                Code = "TOO_MANY_TAGS"
            });
        }

        foreach (var tag in recipe.Tags)
        {
            if (tag.Length > MaxTagLength)
            {
                issues.Add(new ValidationIssue
                {
                    Field = "Tags",
                    Message = $"Tag '{tag[..Math.Min(20, tag.Length)]}...' is too long (max {MaxTagLength})",
                    Severity = ValidationSeverity.Error,
                    Code = "TAG_TOO_LONG"
                });
            }
        }

        // Image URL validation
        if (!string.IsNullOrEmpty(recipe.ImageUrl))
        {
            if (!Uri.TryCreate(recipe.ImageUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                issues.Add(new ValidationIssue
                {
                    Field = "ImageUrl",
                    Message = "Image URL is not a valid HTTP/HTTPS URL",
                    Severity = ValidationSeverity.Error,
                    Code = "INVALID_IMAGE_URL"
                });
            }
        }
    }

    #endregion

    #region Business Validation

    /// <summary>
    /// Validates business rules (mostly produces warnings).
    /// </summary>
    private void ValidateBusinessRules(Recipe recipe, List<ValidationIssue> issues)
    {
        // Unreasonably long prep time
        if (recipe.PrepTimeMinutes > MaxReasonablePrepTimeMinutes)
        {
            issues.Add(new ValidationIssue
            {
                Field = "PrepTimeMinutes",
                Message = $"Prep time of {recipe.PrepTimeMinutes} minutes ({recipe.PrepTimeMinutes / 60} hours) seems unusually long",
                Severity = ValidationSeverity.Warning,
                Code = "LONG_PREP_TIME"
            });
        }

        // Unreasonably long cook time
        if (recipe.CookTimeMinutes > MaxReasonableCookTimeMinutes)
        {
            issues.Add(new ValidationIssue
            {
                Field = "CookTimeMinutes",
                Message = $"Cook time of {recipe.CookTimeMinutes} minutes ({recipe.CookTimeMinutes / 60} hours) seems unusually long",
                Severity = ValidationSeverity.Warning,
                Code = "LONG_COOK_TIME"
            });
        }

        // Zero total time (both prep and cook are 0)
        if (recipe.PrepTimeMinutes == 0 && recipe.CookTimeMinutes == 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "PrepTimeMinutes",
                Message = "Both prep time and cook time are zero - consider adding time estimates",
                Severity = ValidationSeverity.Warning,
                Code = "NO_TIME_ESTIMATES"
            });
        }

        // Unreasonably high servings
        if (recipe.Servings > MaxReasonableServings)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Servings",
                Message = $"Serving size of {recipe.Servings} seems unusually high",
                Severity = ValidationSeverity.Warning,
                Code = "HIGH_SERVINGS"
            });
        }

        // Missing description for complex recipes
        if (string.IsNullOrWhiteSpace(recipe.Description) && 
            recipe.Ingredients.Count >= WarnIfNoDescriptionMinIngredients)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Description",
                Message = "Recipe has no description - consider adding one for better discoverability",
                Severity = ValidationSeverity.Warning,
                Code = "MISSING_DESCRIPTION"
            });
        }

        // Very short description
        if (!string.IsNullOrWhiteSpace(recipe.Description) && recipe.Description.Length < 20)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Description",
                Message = "Recipe description is very short - consider expanding it",
                Severity = ValidationSeverity.Warning,
                Code = "SHORT_DESCRIPTION"
            });
        }

        // No cuisine specified
        if (string.IsNullOrWhiteSpace(recipe.Cuisine))
        {
            issues.Add(new ValidationIssue
            {
                Field = "Cuisine",
                Message = "No cuisine type specified",
                Severity = ValidationSeverity.Warning,
                Code = "MISSING_CUISINE"
            });
        }

        // No tags
        if (recipe.Tags.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Tags",
                Message = "No tags specified - tags help with recipe discovery",
                Severity = ValidationSeverity.Warning,
                Code = "NO_TAGS"
            });
        }

        // No image
        if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
        {
            issues.Add(new ValidationIssue
            {
                Field = "ImageUrl",
                Message = "No image URL specified - recipes with images are more engaging",
                Severity = ValidationSeverity.Warning,
                Code = "NO_IMAGE"
            });
        }

        // Very few ingredients for a recipe with many instructions
        if (recipe.Ingredients.Count < 3 && recipe.Instructions.Count > 5)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Ingredients",
                Message = "Recipe has many instructions but few ingredients - some may be missing",
                Severity = ValidationSeverity.Warning,
                Code = "FEW_INGREDIENTS"
            });
        }

        // Very few instructions for a recipe with many ingredients
        if (recipe.Instructions.Count < 2 && recipe.Ingredients.Count > 5)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Instructions",
                Message = "Recipe has many ingredients but few instructions - some steps may be missing",
                Severity = ValidationSeverity.Warning,
                Code = "FEW_INSTRUCTIONS"
            });
        }

        // Check for duplicate ingredients
        var ingredientNames = recipe.Ingredients
            .Select(i => i.Name.ToLowerInvariant().Trim())
            .ToList();
        var duplicates = ingredientNames
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Ingredients",
                Message = $"Recipe has duplicate ingredients: {string.Join(", ", duplicates)}",
                Severity = ValidationSeverity.Warning,
                Code = "DUPLICATE_INGREDIENTS"
            });
        }

        // Check for ingredients with zero quantity
        var zeroQuantityIngredients = recipe.Ingredients
            .Where(i => i.Quantity == 0)
            .ToList();

        if (zeroQuantityIngredients.Count > 0)
        {
            issues.Add(new ValidationIssue
            {
                Field = "Ingredients",
                Message = $"{zeroQuantityIngredients.Count} ingredient(s) have zero quantity",
                Severity = ValidationSeverity.Warning,
                Code = "ZERO_QUANTITY_INGREDIENTS"
            });
        }
    }

    #endregion

    /// <summary>
    /// Formats a validation issue into a user-friendly string.
    /// </summary>
    private static string FormatIssue(ValidationIssue issue)
    {
        return issue.Code != null 
            ? $"[{issue.Code}] {issue.Field}: {issue.Message}"
            : $"{issue.Field}: {issue.Message}";
    }
}
