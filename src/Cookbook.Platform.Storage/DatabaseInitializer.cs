using System.Text.Json;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Prompts;
using Cookbook.Platform.Shared.Prompts.Templates;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Storage;

/// <summary>
/// Hosted service that initializes the Cosmos DB database and containers on startup.
/// Creates required containers and seeds sample data if the database is empty.
/// </summary>
public class DatabaseInitializer : IHostedService
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosOptions _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    // Container definitions with partition keys
    private static readonly (string Name, string PartitionKey)[] Containers =
    [
        ("recipes", "/id"),
        ("sessions", "/threadId"),
        ("messages", "/threadId"),
        ("tasks", "/taskId"),
        ("artifacts", "/taskId"),
        ("notes", "/threadId"),
        ("prompts", "/phase")
    ];

    public DatabaseInitializer(
        CosmosClient cosmosClient,
        CosmosOptions options,
        ILogger<DatabaseInitializer> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Cosmos DB database: {DatabaseName}", _options.DatabaseName);

        try
        {
            // Create database if it doesn't exist
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                cancellationToken: cancellationToken);

            var database = databaseResponse.Database;
            _logger.LogInformation("Database '{DatabaseName}' ready (Status: {StatusCode})",
                _options.DatabaseName, databaseResponse.StatusCode);

            // Create containers
            foreach (var (name, partitionKey) in Containers)
            {
                var containerResponse = await database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties(name, partitionKey),
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Container '{ContainerName}' ready (Status: {StatusCode})",
                    name, containerResponse.StatusCode);
            }

            // Seed recipes if the container is empty
            await SeedRecipesAsync(database, cancellationToken);

            // Seed prompts if the container is empty
            await SeedPromptsAsync(database, cancellationToken);

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedRecipesAsync(Database database, CancellationToken cancellationToken)
    {
        var container = database.GetContainer("recipes");

        // Check if recipes already exist
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        using var iterator = container.GetItemQueryIterator<int>(query);
        var response = await iterator.ReadNextAsync(cancellationToken);
        var count = response.FirstOrDefault();

        if (count > 0)
        {
            _logger.LogInformation("Recipes container already has {Count} recipes, skipping seed", count);
            return;
        }

        // Load seed data from embedded resource or file
        var seedData = await LoadSeedDataAsync(cancellationToken);
        if (seedData == null || seedData.Count == 0)
        {
            _logger.LogWarning("No seed data found, skipping recipe seeding");
            return;
        }

        _logger.LogInformation("Seeding {Count} recipes...", seedData.Count);

        foreach (var recipe in seedData)
        {
            try
            {
                await container.CreateItemAsync(recipe, new PartitionKey(recipe.Id), cancellationToken: cancellationToken);
                _logger.LogDebug("Seeded recipe: {RecipeId} - {RecipeName}", recipe.Id, recipe.Name);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogDebug("Recipe {RecipeId} already exists, skipping", recipe.Id);
            }
        }

        _logger.LogInformation("Successfully seeded {Count} recipes", seedData.Count);
    }

    private async Task SeedPromptsAsync(Database database, CancellationToken cancellationToken)
    {
        var container = database.GetContainer("prompts");

        // Check if prompts already exist for the Extract phase
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.phase = @phase")
            .WithParameter("@phase", IngestPromptTemplates.ExtractPhase);

        using var iterator = container.GetItemQueryIterator<int>(query);
        var response = await iterator.ReadNextAsync(cancellationToken);
        var count = response.FirstOrDefault();

        if (count > 0)
        {
            _logger.LogInformation("Prompts container already has {Count} extract prompts, skipping seed", count);
            return;
        }

        _logger.LogInformation("Seeding prompt templates...");

        var extractPrompt = new PromptTemplate
        {
            Id = "ingest.extract.v1",
            Name = "Ingest Extract v1",
            Phase = IngestPromptTemplates.ExtractPhase,
            Version = 1,
            IsActive = true,
            SystemPrompt = IngestPromptTemplates.ExtractV1SystemPrompt,
            UserPromptTemplate = IngestPromptTemplates.ExtractV1UserPromptTemplate,
            RequiredVariables = [.. IngestPromptTemplates.ExtractRequiredVariables],
            OptionalVariables = [.. IngestPromptTemplates.ExtractOptionalVariables],
            Constraints = new Dictionary<string, string>
            {
                ["maxContentChars"] = "60000",
                ["outputFormat"] = "json"
            },
            MaxTokens = 4000,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        try
        {
            await container.CreateItemAsync(
                extractPrompt,
                new PartitionKey(extractPrompt.Phase),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Seeded prompt template: {PromptId}", extractPrompt.Id);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogDebug("Prompt {PromptId} already exists, skipping", extractPrompt.Id);
        }

        _logger.LogInformation("Successfully seeded prompt templates");
    }

    private async Task<List<Recipe>?> LoadSeedDataAsync(CancellationToken cancellationToken)
    {
        // Try to load from file system (relative to app directory)
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "seed-recipes.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "seed-recipes.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "seed-recipes.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "data", "seed-recipes.json")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Loading seed data from: {Path}", fullPath);
                var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
                return JsonSerializer.Deserialize<List<Recipe>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }

        // Return embedded sample data as fallback
        _logger.LogInformation("Using embedded sample recipes");
        return GetEmbeddedSampleRecipes();
    }

    private static List<Recipe> GetEmbeddedSampleRecipes()
    {
        return
        [
            new Recipe
            {
                Id = "recipe-001",
                Name = "Classic Spaghetti Carbonara",
                Description = "A creamy Italian pasta dish with eggs, cheese, and pancetta",
                Cuisine = "Italian",
                PrepTimeMinutes = 10,
                CookTimeMinutes = 20,
                Servings = 4,
                Ingredients =
                [
                    new Ingredient { Name = "Spaghetti", Quantity = 400, Unit = "g" },
                    new Ingredient { Name = "Pancetta", Quantity = 200, Unit = "g" },
                    new Ingredient { Name = "Eggs", Quantity = 4, Unit = "whole" },
                    new Ingredient { Name = "Parmesan cheese", Quantity = 100, Unit = "g" },
                    new Ingredient { Name = "Black pepper", Quantity = 1, Unit = "tsp" }
                ],
                Instructions =
                [
                    "Cook spaghetti in salted boiling water until al dente",
                    "While pasta cooks, fry pancetta until crispy",
                    "Beat eggs with grated parmesan and black pepper",
                    "Drain pasta, reserving some cooking water",
                    "Toss hot pasta with pancetta, then remove from heat",
                    "Add egg mixture and toss quickly to create creamy sauce"
                ],
                Tags = ["pasta", "italian", "quick", "comfort food"]
            },
            new Recipe
            {
                Id = "recipe-002",
                Name = "Grilled Chicken Caesar Salad",
                Description = "Fresh romaine lettuce with grilled chicken, croutons, and Caesar dressing",
                Cuisine = "American",
                PrepTimeMinutes = 15,
                CookTimeMinutes = 15,
                Servings = 2,
                Ingredients =
                [
                    new Ingredient { Name = "Chicken breast", Quantity = 2, Unit = "pieces" },
                    new Ingredient { Name = "Romaine lettuce", Quantity = 2, Unit = "heads" },
                    new Ingredient { Name = "Parmesan cheese", Quantity = 50, Unit = "g" },
                    new Ingredient { Name = "Croutons", Quantity = 1, Unit = "cup" },
                    new Ingredient { Name = "Caesar dressing", Quantity = 0.5, Unit = "cup" }
                ],
                Instructions =
                [
                    "Season chicken breasts with salt and pepper",
                    "Grill chicken for 6-7 minutes per side until cooked through",
                    "Let chicken rest for 5 minutes, then slice",
                    "Chop romaine lettuce and place in a large bowl",
                    "Add croutons and shaved parmesan",
                    "Top with sliced chicken",
                    "Drizzle with Caesar dressing and toss to combine"
                ],
                Tags = ["salad", "healthy", "chicken", "lunch"]
            },
            new Recipe
            {
                Id = "recipe-003",
                Name = "Vegetarian Buddha Bowl",
                Description = "A nourishing bowl with quinoa, roasted vegetables, and tahini dressing",
                Cuisine = "American",
                DietType = "vegetarian",
                PrepTimeMinutes = 15,
                CookTimeMinutes = 30,
                Servings = 2,
                Ingredients =
                [
                    new Ingredient { Name = "Quinoa", Quantity = 1, Unit = "cup" },
                    new Ingredient { Name = "Sweet potato", Quantity = 2, Unit = "medium" },
                    new Ingredient { Name = "Chickpeas", Quantity = 1, Unit = "can" },
                    new Ingredient { Name = "Kale", Quantity = 2, Unit = "cups" },
                    new Ingredient { Name = "Avocado", Quantity = 1, Unit = "whole" },
                    new Ingredient { Name = "Tahini", Quantity = 3, Unit = "tbsp" }
                ],
                Instructions =
                [
                    "Cook quinoa according to package directions",
                    "Cube sweet potatoes and roast at 400°F for 25 minutes",
                    "Drain and rinse chickpeas, toss with olive oil and roast for 20 minutes",
                    "Massage kale with olive oil and salt",
                    "Make dressing: whisk tahini, lemon juice, and water",
                    "Assemble bowls with quinoa, vegetables, and sliced avocado",
                    "Drizzle with tahini dressing"
                ],
                Tags = ["vegetarian", "healthy", "bowl", "vegan-optional"]
            }
        ];
    }
}
