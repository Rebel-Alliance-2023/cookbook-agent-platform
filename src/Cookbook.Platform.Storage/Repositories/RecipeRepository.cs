using Cookbook.Platform.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;

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
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds recipes by their source URL hash for duplicate detection.
    /// </summary>
    /// <param name="urlHash">The base64url-encoded hash of the normalized source URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recipes matching the URL hash.</returns>
    public async Task<List<Recipe>> FindByUrlHashAsync(string urlHash, CancellationToken cancellationToken = default)
    {
        // Query for recipes where source.urlHash matches
        var query = new QueryDefinition("SELECT * FROM c WHERE c.source.urlHash = @urlHash")
            .WithParameter("@urlHash", urlHash);

        var results = new List<Recipe>();
        using var iterator = _container.GetItemQueryIterator<Recipe>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogDebug("Found {Count} recipes with URL hash {UrlHash}", results.Count, urlHash);
        return results;
    }

    /// <summary>
    /// Checks if a recipe with the given URL hash already exists.
    /// </summary>
    /// <param name="urlHash">The base64url-encoded hash of the normalized source URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing recipe if found, null otherwise.</returns>
    public async Task<Recipe?> FindDuplicateByUrlHashAsync(string urlHash, CancellationToken cancellationToken = default)
    {
        var duplicates = await FindByUrlHashAsync(urlHash, cancellationToken);
        return duplicates.FirstOrDefault();
    }

    public async Task<List<Recipe>> SearchAsync(string? query = null, string? diet = null, string? cuisine = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        var queryBuilder = new List<string> { "SELECT TOP @limit * FROM c WHERE 1=1" };
        var parameters = new List<(string, object)> { ("@limit", limit) };

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Use lowercase property names to match JSON serialization (JsonPropertyName attributes)
            queryBuilder.Add("AND (CONTAINS(LOWER(c.name), LOWER(@query)) OR CONTAINS(LOWER(c.description), LOWER(@query)))");
            parameters.Add(("@query", query));
        }

        if (!string.IsNullOrWhiteSpace(diet))
        {
            queryBuilder.Add("AND LOWER(c.dietType) = LOWER(@diet)");
            parameters.Add(("@diet", diet));
        }

        if (!string.IsNullOrWhiteSpace(cuisine))
        {
            queryBuilder.Add("AND LOWER(c.cuisine) = LOWER(@cuisine)");
            parameters.Add(("@cuisine", cuisine));
        }

        var queryText = string.Join(" ", queryBuilder);
        _logger.LogInformation("Executing Cosmos query: {Query} with parameters: {Parameters}", 
            queryText, string.Join(", ", parameters.Select(p => $"{p.Item1}={p.Item2}")));

        var queryDef = new QueryDefinition(queryText);
        foreach (var (name, value) in parameters)
        {
            queryDef = queryDef.WithParameter(name, value);
        }

        var results = new List<Recipe>();
        using var iterator = _container.GetItemQueryIterator<Recipe>(queryDef);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            _logger.LogInformation("Cosmos returned {Count} items in this batch", response.Count);
            results.AddRange(response);
        }

        _logger.LogInformation("Total recipes found: {Count}", results.Count);
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
