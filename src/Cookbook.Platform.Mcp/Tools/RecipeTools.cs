using System.ComponentModel;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage.Repositories;
using ModelContextProtocol.Server;

namespace Cookbook.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for recipe operations.
/// </summary>
[McpServerToolType]
public class RecipeTools
{
    private readonly RecipeRepository _recipeRepository;
    private readonly NotesRepository _notesRepository;

    public RecipeTools(RecipeRepository recipeRepository, NotesRepository notesRepository)
    {
        _recipeRepository = recipeRepository;
        _notesRepository = notesRepository;
    }

    /// <summary>
    /// Gets a recipe by its ID.
    /// </summary>
    [McpServerTool(Name = "recipe_get")]
    [Description("Gets a recipe by its ID with full details including ingredients and instructions.")]
    public async Task<Recipe?> GetRecipe(
        [Description("The unique identifier of the recipe")] string id,
        CancellationToken cancellationToken = default)
    {
        return await _recipeRepository.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Saves notes for a recipe research session.
    /// </summary>
    [McpServerTool(Name = "recipe_save_notes")]
    [Description("Saves research notes for a recipe in the current thread.")]
    public async Task<Notes> SaveNotes(
        [Description("The thread ID for the current session")] string threadId,
        [Description("The notes content to save")] string notes,
        [Description("Optional recipe ID these notes relate to")] string? recipeId = null,
        CancellationToken cancellationToken = default)
    {
        var notesEntity = new Notes
        {
            Id = Guid.NewGuid().ToString(),
            ThreadId = threadId,
            Content = notes,
            RecipeId = recipeId,
            CreatedAt = DateTime.UtcNow
        };

        return await _notesRepository.CreateAsync(notesEntity, cancellationToken);
    }

    /// <summary>
    /// Gets all notes for a thread.
    /// </summary>
    [McpServerTool(Name = "recipe_get_notes")]
    [Description("Gets all saved notes for a research session thread.")]
    public async Task<List<Notes>> GetNotes(
        [Description("The thread ID for the session")] string threadId,
        CancellationToken cancellationToken = default)
    {
        return await _notesRepository.GetByThreadIdAsync(threadId, cancellationToken);
    }
}
