using System.Text.Json;
using System.Text.RegularExpressions;
using Cookbook.Platform.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Extracts recipes from Schema.org JSON-LD structured data.
/// </summary>
public partial class JsonLdRecipeExtractor : IRecipeExtractor
{
    private readonly ILogger<JsonLdRecipeExtractor> _logger;

    public ExtractionMethod Method => ExtractionMethod.JsonLd;

    public JsonLdRecipeExtractor(ILogger<JsonLdRecipeExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if the content appears to be JSON-LD.
    /// </summary>
    public bool CanExtract(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var trimmed = content.Trim();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    /// <summary>
    /// Extracts a recipe from JSON-LD content.
    /// </summary>
    public Task<ExtractionResult> ExtractAsync(string content, ExtractionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ExtractionResult.Failed("Content is empty", "EMPTY_CONTENT", ExtractionMethod.JsonLd));
        }

        try
        {
            _logger.LogDebug("Attempting JSON-LD extraction from content ({Length} chars)", content.Length);

            using var doc = JsonDocument.Parse(content);
            var recipe = MapToRecipe(doc.RootElement, context);

            if (recipe == null)
            {
                return Task.FromResult(ExtractionResult.Failed(
                    "Could not map JSON-LD to Recipe model",
                    "MAPPING_FAILED",
                    ExtractionMethod.JsonLd));
            }

            _logger.LogInformation("Successfully extracted recipe '{Name}' from JSON-LD", recipe.Name);

            return Task.FromResult(ExtractionResult.Succeeded(
                recipe,
                ExtractionMethod.JsonLd,
                confidence: 0.95,
                rawSource: content));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON-LD content");
            return Task.FromResult(ExtractionResult.Failed(
                $"Invalid JSON: {ex.Message}",
                "INVALID_JSON",
                ExtractionMethod.JsonLd));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JSON-LD extraction");
            return Task.FromResult(ExtractionResult.Failed(
                $"Extraction error: {ex.Message}",
                "EXTRACTION_ERROR",
                ExtractionMethod.JsonLd));
        }
    }

    /// <summary>
    /// Maps a JSON element to a Recipe.
    /// </summary>
    private Recipe? MapToRecipe(JsonElement element, ExtractionContext context)
    {
        // Generate a temporary ID for the draft
        var id = $"draft-{Guid.NewGuid():N}";

        // Extract required fields
        var name = GetStringProperty(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Recipe JSON-LD missing required 'name' field");
            return null;
        }

        // Extract optional fields
        var description = GetStringProperty(element, "description");
        var prepTime = ParseIsoDuration(GetStringProperty(element, "prepTime"));
        var cookTime = ParseIsoDuration(GetStringProperty(element, "cookTime"));
        var totalTime = ParseIsoDuration(GetStringProperty(element, "totalTime"));
        
        // If only totalTime is specified, split between prep and cook
        if (prepTime == 0 && cookTime == 0 && totalTime > 0)
        {
            prepTime = totalTime / 3;
            cookTime = totalTime - prepTime;
        }

        var servings = ParseServings(element);
        var ingredients = ParseIngredients(element);
        var instructions = ParseInstructions(element);
        var nutrition = ParseNutrition(element);
        var imageUrl = GetImageUrl(element);
        var cuisine = GetStringProperty(element, "recipeCuisine");
        var category = GetStringProperty(element, "recipeCategory");

        // Build tags from category and keywords
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
            tags.Add(category);
        
        var keywords = GetStringProperty(element, "keywords");
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            tags.AddRange(keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return new Recipe
        {
            Id = id,
            Name = name,
            Description = description,
            PrepTimeMinutes = prepTime,
            CookTimeMinutes = cookTime,
            Servings = servings,
            Ingredients = ingredients,
            Instructions = instructions,
            Nutrition = nutrition,
            ImageUrl = imageUrl,
            Cuisine = cuisine,
            Tags = tags.Distinct().ToList(),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parses ISO 8601 duration to minutes.
    /// </summary>
    private static int ParseIsoDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        // Match ISO 8601 duration format: PT1H30M, PT45M, PT2H, etc.
        var match = IsoDurationRegex().Match(duration);
        if (!match.Success)
            return 0;

        var hours = 0;
        var minutes = 0;

        if (match.Groups["hours"].Success)
            int.TryParse(match.Groups["hours"].Value, out hours);
        
        if (match.Groups["minutes"].Success)
            int.TryParse(match.Groups["minutes"].Value, out minutes);

        return (hours * 60) + minutes;
    }

    /// <summary>
    /// Parses servings from various formats.
    /// </summary>
    private static int ParseServings(JsonElement element)
    {
        // Try recipeYield first
        if (element.TryGetProperty("recipeYield", out var yieldProp))
        {
            if (yieldProp.ValueKind == JsonValueKind.Number)
            {
                return yieldProp.GetInt32();
            }
            
            if (yieldProp.ValueKind == JsonValueKind.String)
            {
                var yieldStr = yieldProp.GetString();
                if (!string.IsNullOrEmpty(yieldStr))
                {
                    // Extract first number from string like "4 servings" or "Makes 6"
                    var match = Regex.Match(yieldStr, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out var servings))
                        return servings;
                }
            }
            
            if (yieldProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in yieldProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number)
                        return item.GetInt32();
                    
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var match = Regex.Match(item.GetString() ?? "", @"\d+");
                        if (match.Success && int.TryParse(match.Value, out var servings))
                            return servings;
                    }
                }
            }
        }

        return 4; // Default servings
    }

    /// <summary>
    /// Parses ingredients from recipeIngredient array.
    /// </summary>
    private List<Ingredient> ParseIngredients(JsonElement element)
    {
        var ingredients = new List<Ingredient>();

        if (!element.TryGetProperty("recipeIngredient", out var ingredientsProp))
            return ingredients;

        if (ingredientsProp.ValueKind != JsonValueKind.Array)
            return ingredients;

        foreach (var item in ingredientsProp.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var ingredientStr = item.GetString();
                if (!string.IsNullOrWhiteSpace(ingredientStr))
                {
                    var parsed = ParseIngredientString(ingredientStr);
                    ingredients.Add(parsed);
                }
            }
        }

        return ingredients;
    }

    /// <summary>
    /// Parses an ingredient string into structured data.
    /// Handles formats like "1 cup flour", "2 large eggs", "1/2 tsp salt"
    /// </summary>
    private static Ingredient ParseIngredientString(string ingredientStr)
    {
        var original = ingredientStr.Trim();
        
        // Try to match: [quantity] [unit] [ingredient] [(notes)]
        var match = IngredientRegex().Match(original);
        
        if (match.Success)
        {
            var quantityStr = match.Groups["qty"].Value;
            var unit = match.Groups["unit"].Value.Trim();
            var name = match.Groups["name"].Value.Trim();
            var notes = match.Groups["notes"].Value.Trim().Trim('(', ')');

            var quantity = ParseQuantity(quantityStr);

            // If no unit was captured but the "unit" looks like part of the name, adjust
            if (string.IsNullOrEmpty(unit) && !string.IsNullOrEmpty(name))
            {
                return new Ingredient
                {
                    Name = name,
                    Quantity = quantity,
                    Unit = null,
                    Notes = string.IsNullOrEmpty(notes) ? null : notes
                };
            }

            return new Ingredient
            {
                Name = name,
                Quantity = quantity,
                Unit = string.IsNullOrEmpty(unit) ? null : unit,
                Notes = string.IsNullOrEmpty(notes) ? null : notes
            };
        }

        // Fallback: just use the whole string as the name
        return new Ingredient
        {
            Name = original,
            Quantity = 1,
            Unit = null,
            Notes = null
        };
    }

    /// <summary>
    /// Parses a quantity string including fractions.
    /// </summary>
    private static double ParseQuantity(string quantityStr)
    {
        if (string.IsNullOrWhiteSpace(quantityStr))
            return 1;

        quantityStr = quantityStr.Trim();

        // Replace unicode fractions
        quantityStr = quantityStr
            .Replace("½", " 1/2")
            .Replace("¼", " 1/4")
            .Replace("¾", " 3/4")
            .Replace("?", " 1/3")
            .Replace("?", " 2/3")
            .Replace("?", " 1/8")
            .Trim();

        // Handle mixed numbers like "1 1/2" or just fractions like "1/2"
        var parts = quantityStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double total = 0;

        foreach (var part in parts)
        {
            if (part.Contains('/'))
            {
                var fractionParts = part.Split('/');
                if (fractionParts.Length == 2 &&
                    double.TryParse(fractionParts[0], out var numerator) &&
                    double.TryParse(fractionParts[1], out var denominator) &&
                    denominator != 0)
                {
                    total += numerator / denominator;
                }
            }
            else if (double.TryParse(part, out var num))
            {
                total += num;
            }
        }

        return total > 0 ? total : 1;
    }

    /// <summary>
    /// Parses instructions from recipeInstructions.
    /// </summary>
    private List<string> ParseInstructions(JsonElement element)
    {
        var instructions = new List<string>();

        if (!element.TryGetProperty("recipeInstructions", out var instructionsProp))
            return instructions;

        switch (instructionsProp.ValueKind)
        {
            case JsonValueKind.String:
                // Single string with all instructions
                var text = instructionsProp.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Split by newlines or numbered steps
                    var steps = Regex.Split(text, @"(?:\r?\n)+|(?<=\.)\s*(?=\d+\.)");
                    instructions.AddRange(steps
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s)));
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in instructionsProp.EnumerateArray())
                {
                    var stepText = ExtractInstructionText(item);
                    if (!string.IsNullOrWhiteSpace(stepText))
                    {
                        instructions.Add(stepText);
                    }
                }
                break;
        }

        return instructions;
    }

    /// <summary>
    /// Extracts text from an instruction element (handles HowToStep, HowToSection, etc.)
    /// </summary>
    private static string? ExtractInstructionText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();

        if (element.ValueKind == JsonValueKind.Object)
        {
            // HowToStep: { "@type": "HowToStep", "text": "..." }
            if (element.TryGetProperty("text", out var textProp))
                return textProp.GetString();

            // Sometimes it's in "description"
            if (element.TryGetProperty("description", out var descProp))
                return descProp.GetString();

            // HowToSection with itemListElement
            if (element.TryGetProperty("itemListElement", out var items) && 
                items.ValueKind == JsonValueKind.Array)
            {
                var steps = new List<string>();
                foreach (var item in items.EnumerateArray())
                {
                    var step = ExtractInstructionText(item);
                    if (!string.IsNullOrEmpty(step))
                        steps.Add(step);
                }
                return string.Join(" ", steps);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses nutrition information.
    /// </summary>
    private static NutritionInfo? ParseNutrition(JsonElement element)
    {
        if (!element.TryGetProperty("nutrition", out var nutritionProp) ||
            nutritionProp.ValueKind != JsonValueKind.Object)
            return null;

        return new NutritionInfo
        {
            Calories = ParseNutritionValue(nutritionProp, "calories"),
            ProteinGrams = ParseNutritionValue(nutritionProp, "proteinContent"),
            CarbsGrams = ParseNutritionValue(nutritionProp, "carbohydrateContent"),
            FatGrams = ParseNutritionValue(nutritionProp, "fatContent"),
            FiberGrams = ParseNutritionValue(nutritionProp, "fiberContent"),
            SugarGrams = ParseNutritionValue(nutritionProp, "sugarContent"),
            SodiumMg = ParseNutritionValue(nutritionProp, "sodiumContent")
        };
    }

    /// <summary>
    /// Parses a nutrition value, handling "500 kcal" or "20g" formats.
    /// </summary>
    private static double ParseNutritionValue(JsonElement nutrition, string propertyName)
    {
        if (!nutrition.TryGetProperty(propertyName, out var prop))
            return 0;

        if (prop.ValueKind == JsonValueKind.Number)
            return prop.GetDouble();

        if (prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString() ?? "";
            var match = Regex.Match(str, @"[\d.]+");
            if (match.Success && double.TryParse(match.Value, out var value))
                return value;
        }

        return 0;
    }

    /// <summary>
    /// Gets the image URL from various formats.
    /// </summary>
    private static string? GetImageUrl(JsonElement element)
    {
        if (!element.TryGetProperty("image", out var imageProp))
            return null;

        switch (imageProp.ValueKind)
        {
            case JsonValueKind.String:
                return imageProp.GetString();

            case JsonValueKind.Array:
                foreach (var item in imageProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        return item.GetString();
                    
                    if (item.ValueKind == JsonValueKind.Object && 
                        item.TryGetProperty("url", out var urlProp))
                        return urlProp.GetString();
                }
                break;

            case JsonValueKind.Object:
                if (imageProp.TryGetProperty("url", out var url))
                    return url.GetString();
                break;
        }

        return null;
    }

    /// <summary>
    /// Gets a string property from a JSON element.
    /// </summary>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && 
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    #region Regex Patterns

    [GeneratedRegex(@"^PT(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?", RegexOptions.IgnoreCase)]
    private static partial Regex IsoDurationRegex();

    [GeneratedRegex(@"^(?<qty>[\d\s/½¼¾???]+)?\s*(?<unit>cups?|tablespoons?|tbsp?|teaspoons?|tsp?|ounces?|oz|pounds?|lbs?|grams?|g|kilograms?|kg|milliliters?|ml|liters?|l|pinch|dash|cloves?|slices?|pieces?|stalks?|heads?|bunches?|cans?|packages?|pkg|large|medium|small)?\s*(?<name>[^(]+?)(?:\s*(?<notes>\([^)]+\)))?$", RegexOptions.IgnoreCase)]
    private static partial Regex IngredientRegex();

    #endregion
}
