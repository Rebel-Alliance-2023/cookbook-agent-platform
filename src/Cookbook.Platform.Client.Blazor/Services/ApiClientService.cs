using System.Net.Http.Json;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Models.Ingest.Search;

namespace Cookbook.Platform.Client.Blazor.Services;

/// <summary>
/// Service for interacting with the Gateway API.
/// </summary>
public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(IHttpClientFactory httpClientFactory, ILogger<ApiClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("GatewayApi");

    /// <summary>
    /// Gets the Gateway API base URL for constructing download links.
    /// </summary>
    public string GetGatewayBaseUrl()
    {
        var client = CreateClient();
        return client.BaseAddress?.ToString().TrimEnd('/') ?? "";
    }

    /// <summary>
    /// Downloads an artifact and returns its content.
    /// </summary>
    public async Task<byte[]?> DownloadArtifactAsync(string taskId, string fileName)
    {
        var client = CreateClient();
        try
        {
            var response = await client.GetAsync($"/api/artifacts/{taskId}/{fileName}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download artifact {FileName} for task {TaskId}", fileName, taskId);
        }
        return null;
    }

    /// <summary>
    /// Creates a new session and returns the thread ID.
    /// </summary>
    public async Task<string> CreateSessionAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/sessions/", null);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        _logger.LogInformation("Created session with thread ID: {ThreadId}", session?.ThreadId);
        return session?.ThreadId ?? throw new InvalidOperationException("Failed to create session");
    }

    /// <summary>
    /// Creates a new task for an agent.
    /// </summary>
    public async Task<TaskResponse> CreateTaskAsync(string threadId, string agentType, string query)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/tasks/", new
        {
            ThreadId = threadId,
            AgentType = agentType,
            Query = query
        });
        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        _logger.LogInformation("Created task {TaskId} for agent {AgentType}", task?.TaskId, agentType);
        return task ?? throw new InvalidOperationException("Failed to create task");
    }

    /// <summary>
    /// Creates a new ingest task for importing a recipe from URL.
    /// </summary>
    public async Task<CreateIngestTaskResponse> CreateIngestTaskAsync(string url, string? threadId = null)
    {
        var client = CreateClient();
        var request = new
        {
            AgentType = "Ingest",
            ThreadId = threadId,
            Payload = new
            {
                Mode = "Url",
                Url = url
            }
        };

        var response = await client.PostAsJsonAsync("/api/tasks/ingest", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateIngestTaskResponse>();
        _logger.LogInformation("Created ingest task {TaskId} for URL {Url}", result?.TaskId, url);
        return result ?? throw new InvalidOperationException("Failed to create ingest task");
    }

    /// <summary>
    /// Creates a new ingest task for searching and importing a recipe via query.
    /// </summary>
    public async Task<CreateIngestTaskResponse> CreateSearchIngestTaskAsync(
        string query, 
        string? searchProviderId = null, 
        string? threadId = null)
    {
        var client = CreateClient();
        var request = new
        {
            AgentType = "Ingest",
            ThreadId = threadId,
            Payload = new
            {
                Mode = "Query",
                Query = query,
                Search = new
                {
                    ProviderId = searchProviderId
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/tasks/ingest", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateIngestTaskResponse>();
        _logger.LogInformation("Created search task {TaskId} for query '{Query}' with provider {ProviderId}", 
            result?.TaskId, query, searchProviderId);
        return result ?? throw new InvalidOperationException("Failed to create search task");
    }

    /// <summary>
    /// Gets available search providers for recipe discovery.
    /// </summary>
    public async Task<SearchProvidersResponse> GetSearchProvidersAsync()
    {
        var client = CreateClient();
        var response = await client.GetFromJsonAsync<SearchProvidersResponse>("/api/ingest/providers/search");
        return response ?? throw new InvalidOperationException("Failed to get search providers");
    }

    /// <summary>
    /// Searches for recipes.
    /// </summary>
    public async Task<List<RecipeListItem>> SearchRecipesAsync(string? query = null, string? diet = null, string? cuisine = null)
    {
        var client = CreateClient();
        var queryParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(query))
            queryParams.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(diet))
            queryParams.Add($"diet={Uri.EscapeDataString(diet)}");
        if (!string.IsNullOrWhiteSpace(cuisine))
            queryParams.Add($"cuisine={Uri.EscapeDataString(cuisine)}");

        var url = "/api/recipes/";
        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var recipes = await client.GetFromJsonAsync<List<RecipeListItem>>(url);
        return recipes ?? [];
    }

    /// <summary>
    /// Gets a recipe by ID.
    /// </summary>
    public async Task<Recipe?> GetRecipeAsync(string id)
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<Recipe>($"/api/recipes/{id}");
    }

    /// <summary>
    /// Gets the current state of a task.
    /// </summary>
    public async Task<TaskStateResponse?> GetTaskStateAsync(string taskId)
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<TaskStateResponse>($"/api/tasks/{taskId}/state");
    }

    /// <summary>
    /// Gets a recipe draft for review.
    /// </summary>
    public async Task<RecipeDraft?> GetRecipeDraftAsync(string taskId)
    {
        var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<RecipeDraft>($"/api/tasks/{taskId}/draft");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Commits a recipe draft, adding it to the cookbook.
    /// </summary>
    public async Task<bool> CommitRecipeDraftAsync(string taskId)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"/api/tasks/{taskId}/commit", null);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Rejects a recipe draft.
    /// </summary>
    public async Task<bool> RejectRecipeDraftAsync(string taskId, string? reason = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync($"/api/tasks/{taskId}/reject", new { Reason = reason });
        return response.IsSuccessStatusCode;
    }

    private record SessionResponse(string Id, string ThreadId);
    
    public record TaskResponse(string TaskId, string ThreadId, string AgentType);
    
    public record TaskStateResponse(string TaskId, string Status, int Progress, string? CurrentPhase);
    
    public record RecipeListItem(
        string Id, 
        string Name, 
        string? Description, 
        string? Cuisine, 
        string? DietType,
        int PrepTimeMinutes,
        int CookTimeMinutes,
        int Servings);
}

