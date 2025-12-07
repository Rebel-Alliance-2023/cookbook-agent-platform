using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Cookbook.Platform.Shared.Models;

/// <summary>
/// Represents a recipe in the cookbook system.
/// </summary>
public record Recipe
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string? Description { get; init; }
    
    [JsonPropertyName("ingredients")]
    [JsonProperty("ingredients")]
    public List<Ingredient> Ingredients { get; init; } = [];
    
    [JsonPropertyName("instructions")]
    [JsonProperty("instructions")]
    public List<string> Instructions { get; init; } = [];
    
    [JsonPropertyName("cuisine")]
    [JsonProperty("cuisine")]
    public string? Cuisine { get; init; }
    
    [JsonPropertyName("dietType")]
    [JsonProperty("dietType")]
    public string? DietType { get; init; }
    
    [JsonPropertyName("prepTimeMinutes")]
    [JsonProperty("prepTimeMinutes")]
    public int PrepTimeMinutes { get; init; }
    
    [JsonPropertyName("cookTimeMinutes")]
    [JsonProperty("cookTimeMinutes")]
    public int CookTimeMinutes { get; init; }
    
    [JsonPropertyName("servings")]
    [JsonProperty("servings")]
    public int Servings { get; init; }
    
    [JsonPropertyName("nutrition")]
    [JsonProperty("nutrition")]
    public NutritionInfo? Nutrition { get; init; }
    
    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public List<string> Tags { get; init; } = [];
    
    [JsonPropertyName("imageUrl")]
    [JsonProperty("imageUrl")]
    public string? ImageUrl { get; init; }
    
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Represents an ingredient with quantity and unit.
/// </summary>
public record Ingredient
{
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public double Quantity { get; init; }
    
    [JsonPropertyName("unit")]
    [JsonProperty("unit")]
    public string? Unit { get; init; }
    
    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Nutritional information for a recipe.
/// </summary>
public record NutritionInfo
{
    [JsonPropertyName("calories")]
    [JsonProperty("calories")]
    public double Calories { get; init; }
    
    [JsonPropertyName("proteinGrams")]
    [JsonProperty("proteinGrams")]
    public double ProteinGrams { get; init; }
    
    [JsonPropertyName("carbsGrams")]
    [JsonProperty("carbsGrams")]
    public double CarbsGrams { get; init; }
    
    [JsonPropertyName("fatGrams")]
    [JsonProperty("fatGrams")]
    public double FatGrams { get; init; }
    
    [JsonPropertyName("fiberGrams")]
    [JsonProperty("fiberGrams")]
    public double FiberGrams { get; init; }
    
    [JsonPropertyName("sodiumMg")]
    [JsonProperty("sodiumMg")]
    public double SodiumMg { get; init; }
    
    [JsonPropertyName("sugarGrams")]
    [JsonProperty("sugarGrams")]
    public double SugarGrams { get; init; }
}
