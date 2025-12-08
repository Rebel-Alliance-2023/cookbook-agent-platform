using System.Text;
using System.Text.Json;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.A2A.Analysis.Services;

/// <summary>
/// Analysis Agent server that normalizes recipes, computes nutrition, and generates artifacts.
/// </summary>
public class AnalysisAgentServer
{
    private readonly RecipeRepository _recipeRepository;
    private readonly ArtifactRepository _artifactRepository;
    private readonly IBlobStorage _blobStorage;
    private readonly ILlmRouter _llmRouter;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<AnalysisAgentServer> _logger;

    public AnalysisAgentServer(
        RecipeRepository recipeRepository,
        ArtifactRepository artifactRepository,
        IBlobStorage blobStorage,
        ILlmRouter llmRouter,
        IMessagingBus messagingBus,
        ILogger<AnalysisAgentServer> logger)
    {
        _recipeRepository = recipeRepository;
        _artifactRepository = artifactRepository;
        _blobStorage = blobStorage;
        _llmRouter = llmRouter;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Executes the analysis phase for a given request.
    /// </summary>
    public async Task<AnalysisResult> ExecuteAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting analysis for task {TaskId}", request.TaskId);
        _logger.LogInformation("Analysis payload: {Payload}", request.Payload);

        // Parse the recipe ID from payload (try multiple property names for compatibility)
        var payloadDoc = JsonDocument.Parse(request.Payload);
        var recipeId = "";
        
        // Try different property name variations
        if (payloadDoc.RootElement.TryGetProperty("RecipeId", out var ridProp1))
        {
            recipeId = ridProp1.GetString() ?? "";
        }
        else if (payloadDoc.RootElement.TryGetProperty("recipeId", out var ridProp2))
        {
            recipeId = ridProp2.GetString() ?? "";
        }
        else if (payloadDoc.RootElement.TryGetProperty("Id", out var ridProp3))
        {
            recipeId = ridProp3.GetString() ?? "";
        }
        else if (payloadDoc.RootElement.TryGetProperty("id", out var ridProp4))
        {
            recipeId = ridProp4.GetString() ?? "";
        }

        _logger.LogInformation("Parsed recipeId: '{RecipeId}'", recipeId);

        if (string.IsNullOrWhiteSpace(recipeId))
        {
            throw new InvalidOperationException($"RecipeId not found in payload: {request.Payload}");
        }

        // Stream progress: Loading recipe
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Loading recipe", 10, cancellationToken);

        // Get the recipe
        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        
        if (recipe == null)
        {
            throw new InvalidOperationException($"Recipe {recipeId} not found");
        }

        // Stream progress: Computing nutrition
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Computing nutrition", 30, cancellationToken);

        // Compute nutrition
        var nutrition = ComputeNutrition(recipe);

        // Stream progress: Generating shopping list
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Generating shopping list", 50, cancellationToken);

        // Generate shopping list using LLM (GPT-4o for structured tasks)
        var shoppingList = await GenerateShoppingListAsync(recipe, cancellationToken);

        // Stream progress: Creating artifacts
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Creating artifacts", 70, cancellationToken);

        // Generate and store artifacts
        var artifactUris = new List<string>();

        // Generate markdown recipe card
        var markdownContent = await GenerateRecipeMarkdownAsync(recipe, nutrition, shoppingList, cancellationToken);
        var markdownUri = await StoreArtifactAsync(
            request.TaskId, 
            $"recipe-{recipeId}.md", 
            markdownContent, 
            "text/markdown", 
            cancellationToken);
        artifactUris.Add(markdownUri);

        // Generate shopping list as JSON
        var shoppingListJson = JsonSerializer.Serialize(shoppingList, new JsonSerializerOptions { WriteIndented = true });
        var shoppingListJsonUri = await StoreArtifactAsync(
            request.TaskId,
            $"shopping-list-{recipeId}.json",
            shoppingListJson,
            "application/json",
            cancellationToken);
        artifactUris.Add(shoppingListJsonUri);

        // Generate shopping list as Markdown (nicely formatted for download)
        var shoppingListMarkdown = GenerateShoppingListMarkdown(recipe.Name, shoppingList);
        var shoppingListMdUri = await StoreArtifactAsync(
            request.TaskId,
            $"shopping-list-{recipeId}.md",
            shoppingListMarkdown,
            "text/markdown",
            cancellationToken);
        artifactUris.Add(shoppingListMdUri);

        // Stream progress: Complete
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Analysis complete", 100, cancellationToken);

        _logger.LogInformation("Analysis completed for task {TaskId}", request.TaskId);

        return new AnalysisResult
        {
            RecipeId = recipeId,
            Nutrition = nutrition,
            ShoppingList = shoppingList,
            ArtifactUris = artifactUris
        };
    }

    private NutritionSummary ComputeNutrition(Recipe recipe)
    {
        _logger.LogInformation("Computing nutrition for recipe {RecipeId} with {IngredientCount} ingredients", 
            recipe.Id, recipe.Ingredients.Count);

        // Simplified nutrition estimation
        var totalCalories = 0.0;
        var totalProtein = 0.0;
        var totalCarbs = 0.0;
        var totalFat = 0.0;

        foreach (var ingredient in recipe.Ingredients)
        {
            _logger.LogDebug("Processing ingredient: {Name}, Quantity: {Qty}", ingredient.Name, ingredient.Quantity);
            var (cal, pro, carb, fat) = EstimateIngredientNutrition(ingredient);
            totalCalories += cal;
            totalProtein += pro;
            totalCarbs += carb;
            totalFat += fat;
        }

        var servings = recipe.Servings > 0 ? recipe.Servings : 1;

        _logger.LogInformation("Nutrition totals - Calories: {Cal}, Protein: {Pro}, Carbs: {Carb}, Fat: {Fat}, Servings: {Servings}",
            totalCalories, totalProtein, totalCarbs, totalFat, servings);

        return new NutritionSummary
        {
            CaloriesPerServing = Math.Round(totalCalories / servings, 1),
            ProteinPerServing = Math.Round(totalProtein / servings, 1),
            CarbsPerServing = Math.Round(totalCarbs / servings, 1),
            FatPerServing = Math.Round(totalFat / servings, 1),
            Servings = servings
        };
    }

    private static (double cal, double pro, double carb, double fat) EstimateIngredientNutrition(Ingredient ingredient)
    {
        var name = ingredient.Name.ToLowerInvariant();
        var qty = ingredient.Quantity;

        return name switch
        {
            var n when n.Contains("chicken") => (qty * 165, qty * 31, 0, qty * 3.6),
            var n when n.Contains("beef") => (qty * 250, qty * 26, 0, qty * 15),
            var n when n.Contains("rice") => (qty * 130, qty * 2.7, qty * 28, qty * 0.3),
            var n when n.Contains("pasta") => (qty * 131, qty * 5, qty * 25, qty * 1.1),
            var n when n.Contains("egg") => (qty * 78, qty * 6, qty * 0.6, qty * 5),
            var n when n.Contains("olive oil") => (qty * 120, 0, 0, qty * 14),
            var n when n.Contains("butter") => (qty * 102, qty * 0.1, 0, qty * 12),
            _ => (qty * 50, qty * 2, qty * 8, qty * 1)
        };
    }

    private async Task<List<ShoppingListItem>> GenerateShoppingListAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        try
        {
            var ingredientsList = string.Join("\n", recipe.Ingredients.Select(i => 
                $"- {i.Quantity} {i.Unit} {i.Name}"));

            var response = await _llmRouter.ChatAsync(new LlmRequest
            {
                Provider = "OpenAI",
                SystemPrompt = @"You are a shopping list organizer. Convert recipe ingredients into a categorized shopping list.
Return a JSON array with items like: [{""name"": ""ingredient"", ""quantity"": 1, ""unit"": ""cup"", ""category"": ""Produce""}]
Categories: Produce, Meat, Dairy, Pantry, Spices, Other",
                Messages =
                [
                    new LlmMessage
                    {
                        Role = "user",
                        Content = $"Recipe: {recipe.Name}\n\nIngredients:\n{ingredientsList}\n\nGenerate shopping list JSON:"
                    }
                ],
                MaxTokens = 1000
            }, cancellationToken);

            // Parse the JSON response
            var content = response.Content.Trim();
            
            // Extract JSON if wrapped in code blocks
            if (content.Contains("```"))
            {
                var start = content.IndexOf('[');
                var end = content.LastIndexOf(']') + 1;
                if (start >= 0 && end > start)
                {
                    content = content.Substring(start, end - start);
                }
            }

            var items = JsonSerializer.Deserialize<List<ShoppingListItem>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate shopping list via LLM, using fallback");
            
            // Fallback: convert ingredients directly
            return recipe.Ingredients.Select(i => new ShoppingListItem
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Category = "Other"
            }).ToList();
        }
    }

    private async Task<string> GenerateRecipeMarkdownAsync(
        Recipe recipe, 
        NutritionSummary nutrition, 
        List<ShoppingListItem> shoppingList,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# {recipe.Name}");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(recipe.Description))
        {
            sb.AppendLine(recipe.Description);
            sb.AppendLine();
        }

        sb.AppendLine("## Details");
        sb.AppendLine();
        sb.AppendLine($"- **Cuisine:** {recipe.Cuisine ?? "N/A"}");
        sb.AppendLine($"- **Diet:** {recipe.DietType ?? "N/A"}");
        sb.AppendLine($"- **Prep Time:** {recipe.PrepTimeMinutes} minutes");
        sb.AppendLine($"- **Cook Time:** {recipe.CookTimeMinutes} minutes");
        sb.AppendLine($"- **Servings:** {recipe.Servings}");
        sb.AppendLine();

        sb.AppendLine("## Nutrition (per serving)");
        sb.AppendLine();
        sb.AppendLine($"- Calories: {nutrition.CaloriesPerServing}");
        sb.AppendLine($"- Protein: {nutrition.ProteinPerServing}g");
        sb.AppendLine($"- Carbs: {nutrition.CarbsPerServing}g");
        sb.AppendLine($"- Fat: {nutrition.FatPerServing}g");
        sb.AppendLine();

        sb.AppendLine("## Ingredients");
        sb.AppendLine();
        foreach (var ingredient in recipe.Ingredients)
        {
            sb.AppendLine($"- {ingredient.Quantity} {ingredient.Unit} {ingredient.Name}");
        }
        sb.AppendLine();

        sb.AppendLine("## Instructions");
        sb.AppendLine();
        for (int i = 0; i < recipe.Instructions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {recipe.Instructions[i]}");
        }
        sb.AppendLine();

        sb.AppendLine("## Shopping List");
        sb.AppendLine();
        var groupedItems = shoppingList.GroupBy(i => i.Category ?? "Other");
        foreach (var group in groupedItems.OrderBy(g => g.Key))
        {
            sb.AppendLine($"### {group.Key}");
            foreach (var item in group)
            {
                sb.AppendLine($"- [ ] {item.Quantity} {item.Unit} {item.Name}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateShoppingListMarkdown(string recipeName, List<ShoppingListItem> shoppingList)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# Shopping List");
        sb.AppendLine();
        sb.AppendLine($"**Recipe:** {recipeName}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:MMMM dd, yyyy}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        var groupedItems = shoppingList.GroupBy(i => i.Category ?? "Other").OrderBy(g => g.Key);
        
        foreach (var group in groupedItems)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            
            foreach (var item in group)
            {
                var quantity = item.Quantity > 0 ? $"{item.Quantity}" : "";
                var unit = !string.IsNullOrWhiteSpace(item.Unit) ? $" {item.Unit}" : "";
                sb.AppendLine($"- [ ] {quantity}{unit} {item.Name}");
            }
            
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by Cookbook Agent Platform*");

        return sb.ToString();
    }

    private async Task<string> StoreArtifactAsync(
        string taskId, 
        string name, 
        string content, 
        string contentType,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var path = $"{taskId}/{name}";
        var uri = await _blobStorage.UploadAsync(path, bytes, contentType, cancellationToken);

        // Save artifact metadata
        await _artifactRepository.CreateAsync(new Artifact
        {
            Id = Guid.NewGuid().ToString(),
            TaskId = taskId,
            Name = name,
            Type = contentType.Contains("markdown") ? ArtifactType.Markdown : ArtifactType.Json,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            BlobUri = uri,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return uri;
    }

    private async Task StreamProgressAsync(string threadId, string taskId, string phase, int progress, CancellationToken cancellationToken)
    {
        await _messagingBus.PublishEventAsync(threadId, new AgentEvent
        {
            EventId = Guid.NewGuid().ToString(),
            ThreadId = threadId,
            EventType = "analysis.progress",
            Payload = JsonSerializer.Serialize(new
            {
                TaskId = taskId,
                Phase = phase,
                Progress = progress
            })
        }, cancellationToken);
    }
}

/// <summary>
/// Request model for analysis operations.
/// </summary>
public record AnalysisRequest
{
    public required string TaskId { get; init; }
    public required string ThreadId { get; init; }
    public required string Payload { get; init; }
}

/// <summary>
/// Result of an analysis operation.
/// </summary>
public record AnalysisResult
{
    public required string RecipeId { get; init; }
    public NutritionSummary? Nutrition { get; init; }
    public List<ShoppingListItem> ShoppingList { get; init; } = [];
    public List<string> ArtifactUris { get; init; } = [];
}

/// <summary>
/// Nutrition summary.
/// </summary>
public record NutritionSummary
{
    public double CaloriesPerServing { get; init; }
    public double ProteinPerServing { get; init; }
    public double CarbsPerServing { get; init; }
    public double FatPerServing { get; init; }
    public int Servings { get; init; }
}

/// <summary>
/// Shopping list item.
/// </summary>
public record ShoppingListItem
{
    public required string Name { get; init; }
    public double Quantity { get; init; }
    public string? Unit { get; init; }
    public string? Category { get; init; }
}
