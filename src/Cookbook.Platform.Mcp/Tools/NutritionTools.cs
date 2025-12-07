using System.ComponentModel;
using Cookbook.Platform.Shared.Models;
using ModelContextProtocol.Server;

namespace Cookbook.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for nutrition calculations.
/// </summary>
[McpServerToolType]
public class NutritionTools
{
    /// <summary>
    /// Computes nutrition information for a list of ingredients.
    /// </summary>
    [McpServerTool(Name = "nutrition_compute")]
    [Description("Computes estimated nutrition information for a list of ingredients.")]
    public Task<NutritionResult> ComputeNutrition(
        [Description("List of ingredients with quantities")] List<IngredientInput> ingredients,
        [Description("Number of servings to calculate per-serving values")] int servings = 1,
        CancellationToken cancellationToken = default)
    {
        // Simplified nutrition calculation based on ingredient names
        // In production, this would call a nutrition API or database
        var totalCalories = 0.0;
        var totalProtein = 0.0;
        var totalCarbs = 0.0;
        var totalFat = 0.0;
        var totalFiber = 0.0;

        foreach (var ingredient in ingredients)
        {
            var (calories, protein, carbs, fat, fiber) = EstimateNutrition(ingredient);
            totalCalories += calories;
            totalProtein += protein;
            totalCarbs += carbs;
            totalFat += fat;
            totalFiber += fiber;
        }

        return Task.FromResult(new NutritionResult
        {
            TotalNutrition = new NutritionInfo
            {
                Calories = Math.Round(totalCalories, 1),
                ProteinGrams = Math.Round(totalProtein, 1),
                CarbsGrams = Math.Round(totalCarbs, 1),
                FatGrams = Math.Round(totalFat, 1),
                FiberGrams = Math.Round(totalFiber, 1),
                SodiumMg = 0,
                SugarGrams = 0
            },
            PerServingNutrition = new NutritionInfo
            {
                Calories = Math.Round(totalCalories / servings, 1),
                ProteinGrams = Math.Round(totalProtein / servings, 1),
                CarbsGrams = Math.Round(totalCarbs / servings, 1),
                FatGrams = Math.Round(totalFat / servings, 1),
                FiberGrams = Math.Round(totalFiber / servings, 1),
                SodiumMg = 0,
                SugarGrams = 0
            },
            Servings = servings,
            IngredientBreakdown = ingredients.Select(i => new IngredientNutrition
            {
                Name = i.Name,
                EstimatedCalories = EstimateNutrition(i).calories
            }).ToList()
        });
    }

    private static (double calories, double protein, double carbs, double fat, double fiber) EstimateNutrition(IngredientInput ingredient)
    {
        // Simplified estimation based on common ingredients
        var name = ingredient.Name.ToLowerInvariant();
        var quantity = ingredient.Quantity;

        return name switch
        {
            var n when n.Contains("chicken") => (quantity * 165, quantity * 31, 0, quantity * 3.6, 0),
            var n when n.Contains("beef") => (quantity * 250, quantity * 26, 0, quantity * 15, 0),
            var n when n.Contains("rice") => (quantity * 130, quantity * 2.7, quantity * 28, quantity * 0.3, quantity * 0.4),
            var n when n.Contains("pasta") => (quantity * 131, quantity * 5, quantity * 25, quantity * 1.1, quantity * 1.8),
            var n when n.Contains("egg") => (quantity * 78, quantity * 6, quantity * 0.6, quantity * 5, 0),
            var n when n.Contains("olive oil") => (quantity * 120, 0, 0, quantity * 14, 0),
            var n when n.Contains("butter") => (quantity * 102, quantity * 0.1, 0, quantity * 12, 0),
            var n when n.Contains("onion") => (quantity * 40, quantity * 1.1, quantity * 9, quantity * 0.1, quantity * 1.7),
            var n when n.Contains("garlic") => (quantity * 4, quantity * 0.2, quantity * 1, 0, quantity * 0.1),
            var n when n.Contains("tomato") => (quantity * 22, quantity * 1.1, quantity * 4.8, quantity * 0.2, quantity * 1.5),
            var n when n.Contains("cheese") => (quantity * 113, quantity * 7, quantity * 0.4, quantity * 9, 0),
            var n when n.Contains("milk") => (quantity * 42, quantity * 3.4, quantity * 5, quantity * 1, 0),
            var n when n.Contains("flour") => (quantity * 364, quantity * 10, quantity * 76, quantity * 1, quantity * 2.7),
            var n when n.Contains("sugar") => (quantity * 387, 0, quantity * 100, 0, 0),
            var n when n.Contains("salt") => (0, 0, 0, 0, 0),
            var n when n.Contains("pepper") => (quantity * 1, 0, quantity * 0.3, 0, quantity * 0.1),
            _ => (quantity * 50, quantity * 2, quantity * 8, quantity * 1, quantity * 1)
        };
    }
}

/// <summary>
/// Input model for ingredient nutrition calculation.
/// </summary>
public record IngredientInput
{
    public required string Name { get; init; }
    public double Quantity { get; init; } = 1;
    public string? Unit { get; init; }
}

/// <summary>
/// Result of nutrition computation.
/// </summary>
public record NutritionResult
{
    public required NutritionInfo TotalNutrition { get; init; }
    public required NutritionInfo PerServingNutrition { get; init; }
    public int Servings { get; init; }
    public List<IngredientNutrition> IngredientBreakdown { get; init; } = [];
}

/// <summary>
/// Per-ingredient nutrition information.
/// </summary>
public record IngredientNutrition
{
    public required string Name { get; init; }
    public double EstimatedCalories { get; init; }
}
