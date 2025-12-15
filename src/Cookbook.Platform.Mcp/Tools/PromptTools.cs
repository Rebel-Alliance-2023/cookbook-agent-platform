using System.ComponentModel;
using Cookbook.Platform.Shared.Models.Prompts;
using Cookbook.Platform.Shared.Prompts;
using Cookbook.Platform.Storage.Repositories;
using ModelContextProtocol.Server;

namespace Cookbook.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for prompt template management and rendering.
/// These tools allow agents to discover, retrieve, and render prompt templates
/// from the prompt registry.
/// </summary>
[McpServerToolType]
public class PromptTools
{
    private readonly IPromptRepository _promptRepository;
    private readonly IPromptRenderer _promptRenderer;

    public PromptTools(IPromptRepository promptRepository, IPromptRenderer promptRenderer)
    {
        _promptRepository = promptRepository;
        _promptRenderer = promptRenderer;
    }

    /// <summary>
    /// Lists all prompt templates, optionally filtered by phase.
    /// </summary>
    [McpServerTool(Name = "prompt_list")]
    [Description("Lists all available prompt templates in the registry, optionally filtered by phase. Returns template metadata including ID, name, phase, version, and active status.")]
    public async Task<PromptListResult> ListPrompts(
        [Description("Optional phase to filter by (e.g., 'Ingest.Extract', 'Ingest.Normalize'). If omitted, returns all templates.")] 
        string? phase = null,
        CancellationToken cancellationToken = default)
    {
        List<PromptTemplate> templates;
        
        if (!string.IsNullOrEmpty(phase))
        {
            templates = await _promptRepository.GetByPhaseAsync(phase, cancellationToken);
        }
        else
        {
            templates = await _promptRepository.GetAllAsync(cancellationToken);
        }

        var summaries = templates.Select(t => new PromptSummary
        {
            Id = t.Id,
            Name = t.Name,
            Phase = t.Phase,
            Version = t.Version,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            RequiredVariables = t.RequiredVariables,
            OptionalVariables = t.OptionalVariables
        }).ToList();

        return new PromptListResult
        {
            Prompts = summaries,
            TotalCount = summaries.Count,
            FilteredByPhase = phase
        };
    }

    /// <summary>
    /// Gets a prompt template by ID and phase.
    /// </summary>
    [McpServerTool(Name = "prompt_get")]
    [Description("Gets a specific prompt template by its ID and phase. Returns the full template including system prompt, user prompt template, and all metadata.")]
    public async Task<PromptTemplate?> GetPrompt(
        [Description("The unique identifier of the prompt template (e.g., 'ingest.extract.v1')")] 
        string id,
        [Description("The phase the prompt belongs to (e.g., 'Ingest.Extract'). Required for efficient lookup.")] 
        string phase,
        CancellationToken cancellationToken = default)
    {
        return await _promptRepository.GetByIdAsync(id, phase, cancellationToken);
    }

    /// <summary>
    /// Gets the active prompt template for a specific phase.
    /// </summary>
    [McpServerTool(Name = "prompt_get_active")]
    [Description("Gets the currently active prompt template for a specific phase. Each phase can have only one active template at a time.")]
    public async Task<PromptTemplate?> GetActivePrompt(
        [Description("The phase to get the active template for (e.g., 'Ingest.Extract', 'Ingest.Normalize', 'Ingest.Repair')")] 
        string phase,
        CancellationToken cancellationToken = default)
    {
        return await _promptRepository.GetActiveByPhaseAsync(phase, cancellationToken);
    }

    /// <summary>
    /// Renders a prompt template with provided variables.
    /// </summary>
    [McpServerTool(Name = "prompt_render")]
    [Description("Renders a prompt template by substituting variables into the template. Returns the rendered system prompt and user prompt ready for LLM consumption.")]
    public async Task<PromptRenderResult> RenderPrompt(
        [Description("The unique identifier of the prompt template to render")] 
        string promptId,
        [Description("The phase the prompt belongs to")] 
        string phase,
        [Description("Dictionary of variable names and values to substitute into the template")] 
        Dictionary<string, object?> variables,
        [Description("Maximum characters for content truncation (optional)")] 
        int? maxCharacters = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _promptRepository.GetByIdAsync(promptId, phase, cancellationToken);
        
        if (template == null)
        {
            return new PromptRenderResult
            {
                Success = false,
                Error = $"Prompt template '{promptId}' not found in phase '{phase}'"
            };
        }

        try
        {
            string renderedUserPrompt;
            
            if (maxCharacters.HasValue)
            {
                renderedUserPrompt = _promptRenderer.RenderWithTruncation(
                    template.UserPromptTemplate,
                    variables,
                    template.RequiredVariables,
                    maxCharacters);
            }
            else
            {
                renderedUserPrompt = _promptRenderer.Render(
                    template.UserPromptTemplate,
                    variables,
                    template.RequiredVariables);
            }

            return new PromptRenderResult
            {
                Success = true,
                SystemPrompt = template.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                PromptId = template.Id,
                Phase = template.Phase,
                Version = template.Version
            };
        }
        catch (PromptRenderException ex)
        {
            return new PromptRenderResult
            {
                Success = false,
                Error = ex.Message,
                PromptId = template.Id,
                Phase = template.Phase
            };
        }
    }
}

/// <summary>
/// Summary information for a prompt template.
/// </summary>
public record PromptSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Phase { get; init; }
    public required int Version { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<string> RequiredVariables { get; init; } = [];
    public List<string> OptionalVariables { get; init; } = [];
}

/// <summary>
/// Result of listing prompt templates.
/// </summary>
public record PromptListResult
{
    public List<PromptSummary> Prompts { get; init; } = [];
    public int TotalCount { get; init; }
    public string? FilteredByPhase { get; init; }
}

/// <summary>
/// Result of rendering a prompt template.
/// </summary>
public record PromptRenderResult
{
    public bool Success { get; init; }
    public string? SystemPrompt { get; init; }
    public string? UserPrompt { get; init; }
    public string? Error { get; init; }
    public string? PromptId { get; init; }
    public string? Phase { get; init; }
    public int? Version { get; init; }
}
