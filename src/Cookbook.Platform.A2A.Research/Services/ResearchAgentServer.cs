using System.Text.Json;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.A2A.Research.Services;

/// <summary>
/// Research Agent server that discovers recipe candidates and saves notes.
/// </summary>
public class ResearchAgentServer
{
    private readonly RecipeRepository _recipeRepository;
    private readonly NotesRepository _notesRepository;
    private readonly ILlmRouter _llmRouter;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<ResearchAgentServer> _logger;

    public ResearchAgentServer(
        RecipeRepository recipeRepository,
        NotesRepository notesRepository,
        ILlmRouter llmRouter,
        IMessagingBus messagingBus,
        ILogger<ResearchAgentServer> logger)
    {
        _recipeRepository = recipeRepository;
        _notesRepository = notesRepository;
        _llmRouter = llmRouter;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Executes the research phase for a given request.
    /// </summary>
    public async Task<ResearchResult> ExecuteAsync(ResearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting research for task {TaskId}", request.TaskId);

        // Parse the query from payload
        _logger.LogInformation("Parsing payload: {Payload}", request.Payload);
        var payloadDoc = JsonDocument.Parse(request.Payload);
        var query = payloadDoc.RootElement.GetProperty("Query").GetString() ?? "";
        _logger.LogInformation("Searching for query: '{Query}'", query);

        // Stream progress: Searching
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Searching for recipes", 10, cancellationToken);

        // Search for recipes using MCP-style tool call simulation
        var recipes = await _recipeRepository.SearchAsync(query, limit: 5, cancellationToken: cancellationToken);
        _logger.LogInformation("Repository returned {Count} recipes for query '{Query}'", recipes.Count, query);

        // Stream progress: Analyzing
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Analyzing candidates", 30, cancellationToken);

        var candidates = new List<RecipeCandidate>();
        
        foreach (var recipe in recipes)
        {
            // Get full recipe details
            var fullRecipe = await _recipeRepository.GetByIdAsync(recipe.Id, cancellationToken);
            
            if (fullRecipe != null)
            {
                // Use LLM to score relevance (Claude for research)
                var score = await ScoreRecipeAsync(query, fullRecipe, cancellationToken);
                
                candidates.Add(new RecipeCandidate
                {
                    Id = fullRecipe.Id,
                    Name = fullRecipe.Name,
                    Description = fullRecipe.Description,
                    Cuisine = fullRecipe.Cuisine,
                    DietType = fullRecipe.DietType,
                    RelevanceScore = score
                });
            }
        }

        // Sort by relevance
        candidates = candidates.OrderByDescending(c => c.RelevanceScore).ToList();

        // Stream progress: Generating notes
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Generating research notes", 70, cancellationToken);

        // Generate research notes using LLM
        var notes = await GenerateNotesAsync(query, candidates, cancellationToken);

        // Save notes
        await _notesRepository.CreateAsync(new Notes
        {
            Id = Guid.NewGuid().ToString(),
            ThreadId = request.ThreadId,
            Content = notes,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // Stream progress: Complete
        await StreamProgressAsync(request.ThreadId, request.TaskId, "Research complete", 100, cancellationToken);

        _logger.LogInformation("Research completed for task {TaskId}, found {Count} candidates", 
            request.TaskId, candidates.Count);

        return new ResearchResult
        {
            Candidates = candidates,
            Notes = notes
        };
    }

    private async Task<double> ScoreRecipeAsync(string query, Recipe recipe, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _llmRouter.ChatAsync(new LlmRequest
            {
                Provider = "Claude",
                SystemPrompt = "You are a recipe relevance scorer. Rate how well a recipe matches a user query on a scale of 0.0 to 1.0. Respond with only the numeric score.",
                Messages =
                [
                    new LlmMessage
                    {
                        Role = "user",
                        Content = $"Query: \"{query}\"\n\nRecipe: {recipe.Name}\nDescription: {recipe.Description}\nCuisine: {recipe.Cuisine}\nIngredients: {string.Join(", ", recipe.Ingredients.Select(i => i.Name))}\n\nScore:"
                    }
                ],
                MaxTokens = 10
            }, cancellationToken);

            _logger.LogInformation("LLM scored recipe {RecipeId} with response: '{Response}'", recipe.Id, response.Content);

            if (double.TryParse(response.Content.Trim(), out var score))
            {
                _logger.LogInformation("Parsed score for {RecipeId}: {Score}", recipe.Id, score);
                return Math.Clamp(score, 0.0, 1.0);
            }
            else
            {
                _logger.LogWarning("Could not parse LLM response as double: '{Response}'", response.Content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to score recipe {RecipeId}", recipe.Id);
        }

        return 0.5; // Default score
    }

    private async Task<string> GenerateNotesAsync(string query, List<RecipeCandidate> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return $"No recipes found matching \"{query}\".";
        }

        try
        {
            var candidatesList = string.Join("\n", candidates.Select((c, i) => 
                $"{i + 1}. {c.Name} (Score: {c.RelevanceScore:F2}) - {c.Description}"));

            var response = await _llmRouter.ChatAsync(new LlmRequest
            {
                Provider = "Claude",
                SystemPrompt = "You are a culinary research assistant. Generate concise research notes summarizing recipe search results.",
                Messages =
                [
                    new LlmMessage
                    {
                        Role = "user",
                        Content = $"User searched for: \"{query}\"\n\nFound candidates:\n{candidatesList}\n\nGenerate brief research notes:"
                    }
                ],
                MaxTokens = 500
            }, cancellationToken);

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate notes");
            return $"Found {candidates.Count} recipes matching \"{query}\".";
        }
    }

    private async Task StreamProgressAsync(string threadId, string taskId, string phase, int progress, CancellationToken cancellationToken)
    {
        await _messagingBus.PublishEventAsync(threadId, new AgentEvent
        {
            EventId = Guid.NewGuid().ToString(),
            ThreadId = threadId,
            EventType = "research.progress",
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
/// Request model for research operations.
/// </summary>
public record ResearchRequest
{
    public required string TaskId { get; init; }
    public required string ThreadId { get; init; }
    public required string Payload { get; init; }
}

/// <summary>
/// Result of a research operation.
/// </summary>
public record ResearchResult
{
    public List<RecipeCandidate> Candidates { get; init; } = [];
    public string? Notes { get; init; }
}

/// <summary>
/// A recipe candidate from research.
/// </summary>
public record RecipeCandidate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Cuisine { get; init; }
    public string? DietType { get; init; }
    public double RelevanceScore { get; init; }
}
