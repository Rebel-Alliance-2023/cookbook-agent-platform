using System.ComponentModel;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage.Repositories;
using ModelContextProtocol.Server;

namespace Cookbook.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for searching recipes.
/// </summary>
[McpServerToolType]
public class SearchTools
{
    private readonly RecipeRepository _recipeRepository;

    public SearchTools(RecipeRepository recipeRepository)
    {
        _recipeRepository = recipeRepository;
    }

    /// <summary>
    /// Searches for recipes based on query, diet type, and cuisine.
    /// </summary>
    [McpServerTool(Name = "search_recipes")]
    [Description("Searches for recipes based on query, diet type, and cuisine.")]
    public async Task<List<RecipeSearchResult>> SearchRecipes(
        [Description("Search query for recipe name or description")] string? query = null,
        [Description("Diet type filter (e.g., vegetarian, vegan, keto)")] string? diet = null,
        [Description("Cuisine type filter (e.g., Italian, Mexican, Asian)")] string? cuisine = null,
        [Description("Maximum number of results to return")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var recipes = await _recipeRepository.SearchAsync(query, diet, cuisine, limit, cancellationToken);
        
        return recipes.Select(r => new RecipeSearchResult
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Cuisine = r.Cuisine,
            DietType = r.DietType,
            PrepTimeMinutes = r.PrepTimeMinutes,
            CookTimeMinutes = r.CookTimeMinutes
        }).ToList();
    }
}

/// <summary>
/// Simplified recipe result for search operations.
/// </summary>
public record RecipeSearchResult
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Cuisine { get; init; }
    public string? DietType { get; init; }
    public int PrepTimeMinutes { get; init; }
    public int CookTimeMinutes { get; init; }
}
