using System.Net.Http.Json;
using System.Text.Json;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Manages the execution of research and analysis phases.
/// </summary>
public class AgentPipeline
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<AgentPipeline> _logger;

    public AgentPipeline(
        IHttpClientFactory httpClientFactory,
        IMessagingBus messagingBus,
        ILogger<AgentPipeline> logger)
    {
        _httpClientFactory = httpClientFactory;
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Executes the research phase by calling the Research Agent.
    /// </summary>
    public async Task<ResearchPhaseResult> ExecuteResearchPhaseAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing research phase for task {TaskId}", task.TaskId);

        // Update progress
        await UpdateProgressAsync(task, "Searching for recipes", 10, cancellationToken);

        var client = _httpClientFactory.CreateClient("ResearchAgent");
        
        try
        {
            // Call the research agent
            var response = await client.PostAsJsonAsync("/api/research", new
            {
                task.TaskId,
                task.ThreadId,
                task.Payload
            }, cancellationToken);

            response.EnsureSuccessStatusCode();

            await UpdateProgressAsync(task, "Retrieving recipe details", 50, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<ResearchPhaseResult>(cancellationToken);

            await UpdateProgressAsync(task, "Saving research notes", 90, cancellationToken);

            return result ?? new ResearchPhaseResult
            {
                Candidates = [],
                Notes = "No results found"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Research agent not available, using fallback");
            
            // Fallback: return empty result
            return new ResearchPhaseResult
            {
                Candidates = [],
                Notes = "Research agent unavailable"
            };
        }
    }

    /// <summary>
    /// Executes the analysis phase by calling the Analysis Agent.
    /// </summary>
    public async Task<AnalysisPhaseResult> ExecuteAnalysisPhaseAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing analysis phase for task {TaskId}", task.TaskId);

        // Update progress
        await UpdateProgressAsync(task, "Normalizing recipe", 10, cancellationToken);

        var client = _httpClientFactory.CreateClient("AnalysisAgent");

        try
        {
            // Call the analysis agent
            var response = await client.PostAsJsonAsync("/api/analyze", new
            {
                task.TaskId,
                task.ThreadId,
                task.Payload
            }, cancellationToken);

            response.EnsureSuccessStatusCode();

            await UpdateProgressAsync(task, "Computing nutrition", 40, cancellationToken);
            await UpdateProgressAsync(task, "Generating shopping list", 70, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<AnalysisPhaseResult>(cancellationToken);

            await UpdateProgressAsync(task, "Storing artifacts", 90, cancellationToken);

            return result ?? new AnalysisPhaseResult
            {
                RecipeId = "",
                ShoppingList = [],
                ArtifactUris = []
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Analysis agent not available, using fallback");
            
            // Fallback: return empty result
            return new AnalysisPhaseResult
            {
                RecipeId = "",
                ShoppingList = [],
                ArtifactUris = []
            };
        }
    }

    private async Task UpdateProgressAsync(AgentTask task, string phase, int progress, CancellationToken cancellationToken)
    {
        await _messagingBus.SetTaskStateAsync(task.TaskId, new TaskState
        {
            TaskId = task.TaskId,
            Status = Shared.Messaging.TaskStatus.Running,
            CurrentPhase = phase,
            Progress = progress
        }, cancellationToken: cancellationToken);

        await _messagingBus.PublishEventAsync(task.ThreadId, new AgentEvent
        {
            EventId = Guid.NewGuid().ToString(),
            ThreadId = task.ThreadId,
            EventType = "task.progress",
            Payload = JsonSerializer.Serialize(new
            {
                task.TaskId,
                Phase = phase,
                Progress = progress
            })
        }, cancellationToken);
    }
}

/// <summary>
/// Result of the research phase.
/// </summary>
public record ResearchPhaseResult
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
    public double Score { get; init; }
}

/// <summary>
/// Result of the analysis phase.
/// </summary>
public record AnalysisPhaseResult
{
    public required string RecipeId { get; init; }
    public List<ShoppingListItem> ShoppingList { get; init; } = [];
    public NutritionSummary? Nutrition { get; init; }
    public List<string> ArtifactUris { get; init; } = [];
}

/// <summary>
/// A shopping list item.
/// </summary>
public record ShoppingListItem
{
    public required string Name { get; init; }
    public double Quantity { get; init; }
    public string? Unit { get; init; }
    public string? Category { get; init; }
}

/// <summary>
/// Nutrition summary for analysis.
/// </summary>
public record NutritionSummary
{
    public double Calories { get; init; }
    public double Protein { get; init; }
    public double Carbs { get; init; }
    public double Fat { get; init; }
}
