using Cookbook.Platform.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Storage.Repositories;

/// <summary>
/// Repository for Recipe entities in Cosmos DB.
/// </summary>
public class RecipeRepository
{
    private readonly Container _container;
    private readonly ILogger<RecipeRepository> _logger;

    public RecipeRepository(CosmosClient cosmosClient, CosmosOptions options, ILogger<RecipeRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "recipes");
        _logger = logger;
    }

    public async Task<Recipe?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<Recipe>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Recipe>> SearchAsync(string? query = null, string? diet = null, string? cuisine = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new List<string> { "SELECT TOP @limit * FROM c WHERE 1=1" };
        var parameters = new List<(string, object)> { ("@limit", limit) };

        if (!string.IsNullOrWhiteSpace(query))
        {
            queryBuilder.Add("AND (CONTAINS(LOWER(c.Name), LOWER(@query)) OR CONTAINS(LOWER(c.Description), LOWER(@query)))");
            parameters.Add(("@query", query));
        }

        if (!string.IsNullOrWhiteSpace(diet))
        {
            queryBuilder.Add("AND LOWER(c.DietType) = LOWER(@diet)");
            parameters.Add(("@diet", diet));
        }

        if (!string.IsNullOrWhiteSpace(cuisine))
        {
            queryBuilder.Add("AND LOWER(c.Cuisine) = LOWER(@cuisine)");
            parameters.Add(("@cuisine", cuisine));
        }

        var queryDef = new QueryDefinition(string.Join(" ", queryBuilder));
        foreach (var (name, value) in parameters)
        {
            queryDef = queryDef.WithParameter(name, value);
        }

        var results = new List<Recipe>();
        using var iterator = _container.GetItemQueryIterator<Recipe>(queryDef);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Recipe> CreateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(recipe, new PartitionKey(recipe.Id), cancellationToken: cancellationToken);
        _logger.LogInformation("Created recipe {RecipeId}: {RecipeName}", recipe.Id, recipe.Name);
        return response.Resource;
    }

    public async Task<Recipe> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        var updatedRecipe = recipe with { UpdatedAt = DateTime.UtcNow };
        var response = await _container.ReplaceItemAsync(updatedRecipe, recipe.Id, new PartitionKey(recipe.Id), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _container.DeleteItemAsync<Recipe>(id, new PartitionKey(id), cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted recipe {RecipeId}", id);
    }
}
