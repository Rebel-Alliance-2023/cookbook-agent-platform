using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage.Repositories;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Recipe management endpoints.
/// </summary>
public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes")
            .WithTags("Recipes");

        group.MapGet("/", SearchRecipes)
            .WithName("SearchRecipes")
            .WithSummary("Searches for recipes");

        group.MapGet("/{id}", GetRecipe)
            .WithName("GetRecipe")
            .WithSummary("Gets a recipe by ID");

        group.MapPost("/", CreateRecipe)
            .WithName("CreateRecipe")
            .WithSummary("Creates a new recipe");

        return endpoints;
    }

    private static async Task<IResult> SearchRecipes(
        RecipeRepository recipeRepository,
        string? query = null,
        string? diet = null,
        string? cuisine = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var recipes = await recipeRepository.SearchAsync(query, diet, cuisine, limit, cancellationToken);
        return Results.Ok(recipes);
    }

    private static async Task<IResult> GetRecipe(
        string id,
        RecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdAsync(id, cancellationToken);
        return recipe is null ? Results.NotFound() : Results.Ok(recipe);
    }

    private static async Task<IResult> CreateRecipe(
        Recipe recipe,
        RecipeRepository recipeRepository,
        CancellationToken cancellationToken)
    {
        var created = await recipeRepository.CreateAsync(recipe, cancellationToken);
        return Results.Created($"/api/recipes/{created.Id}", created);
    }
}
