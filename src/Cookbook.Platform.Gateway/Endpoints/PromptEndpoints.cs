using Cookbook.Platform.Shared.Models.Prompts;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Prompt template registry endpoints for managing LLM prompt templates.
/// </summary>
public static class PromptEndpoints
{
    /// <summary>
    /// Maps prompt registry endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapPromptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/prompts")
            .WithTags("Prompts");

        // Read endpoints - no special authorization required
        var readGroup = group.MapGroup("/");

        // M0-031: GET /api/prompts?phase=...
        readGroup.MapGet("/", GetPrompts)
            .WithName("GetPrompts")
            .WithSummary("Gets prompt templates, optionally filtered by phase")
            .WithDescription("Returns all prompt templates for a phase, or all templates if no phase is specified.");

        // M0-032: GET /api/prompts/{id}?phase=...
        readGroup.MapGet("/{id}", GetPromptById)
            .WithName("GetPromptById")
            .WithSummary("Gets a prompt template by ID")
            .WithDescription("Requires phase query parameter for efficient lookup. Returns 404 if not found.");

        // Additional endpoint: Get active prompt for a phase
        readGroup.MapGet("/active", GetActivePrompt)
            .WithName("GetActivePrompt")
            .WithSummary("Gets the active prompt template for a phase")
            .WithDescription("Returns the currently active prompt template for the specified phase.");

        // Write endpoints - require admin authorization
        // M0-036: Admin authorization applied via RequireAuthorization when auth is configured
        var adminGroup = group.MapGroup("/")
            .WithMetadata(new PromptAdminEndpointAttribute());

        // M0-033: POST /api/prompts
        adminGroup.MapPost("/", CreatePrompt)
            .WithName("CreatePrompt")
            .WithSummary("Creates a new prompt template")
            .WithDescription("Creates a new prompt template. The template will not be active by default. Requires admin access.");

        // M0-034: POST /api/prompts/{id}/activate
        adminGroup.MapPost("/{id}/activate", ActivatePrompt)
            .WithName("ActivatePrompt")
            .WithSummary("Activates a prompt template")
            .WithDescription("Activates the specified template, deactivating any other active template for the same phase. Requires admin access.");

        // M0-035: POST /api/prompts/{id}/deactivate
        adminGroup.MapPost("/{id}/deactivate", DeactivatePrompt)
            .WithName("DeactivatePrompt")
            .WithSummary("Deactivates a prompt template")
            .WithDescription("Deactivates the specified template. Requires admin access.");

        return endpoints;
    }

    /// <summary>
    /// Gets prompt templates, optionally filtered by phase.
    /// GET /api/prompts?phase=...
    /// </summary>
    private static async Task<IResult> GetPrompts(
        IPromptRepository promptRepository,
        [FromQuery] string? phase = null,
        CancellationToken cancellationToken = default)
    {
        List<PromptTemplate> templates;

        if (!string.IsNullOrEmpty(phase))
        {
            templates = await promptRepository.GetByPhaseAsync(phase, cancellationToken);
        }
        else
        {
            templates = await promptRepository.GetAllAsync(cancellationToken);
        }

        return Results.Ok(templates);
    }

    /// <summary>
    /// Gets a prompt template by ID.
    /// GET /api/prompts/{id}?phase=...
    /// </summary>
    private static async Task<IResult> GetPromptById(
        string id,
        IPromptRepository promptRepository,
        [FromQuery] string? phase = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(phase))
        {
            // Without phase, we need to do a cross-partition query
            // This is less efficient but acceptable at small scale
            var allTemplates = await promptRepository.GetAllAsync(cancellationToken);
            var template = allTemplates.FirstOrDefault(t => t.Id == id);
            return template is null ? Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = $"Prompt template '{id}' not found" }) : Results.Ok(template);
        }

        var result = await promptRepository.GetByIdAsync(id, phase, cancellationToken);
        return result is null
            ? Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = $"Prompt template '{id}' not found in phase '{phase}'" })
            : Results.Ok(result);
    }

    /// <summary>
    /// Gets the active prompt template for a phase.
    /// GET /api/prompts/active?phase=...
    /// </summary>
    private static async Task<IResult> GetActivePrompt(
        IPromptRepository promptRepository,
        [FromQuery] string phase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE", message = "Phase query parameter is required" });
        }

        var template = await promptRepository.GetActiveByPhaseAsync(phase, cancellationToken);
        return template is null
            ? Results.NotFound(new { error = "NO_ACTIVE_PROMPT", message = $"No active prompt template found for phase '{phase}'" })
            : Results.Ok(template);
    }

    /// <summary>
    /// Creates a new prompt template.
    /// POST /api/prompts
    /// </summary>
    private static async Task<IResult> CreatePrompt(
        CreatePromptRequest request,
        IPromptRepository promptRepository,
        CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(request.Id))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST", message = "Id is required" });
        }
        if (string.IsNullOrEmpty(request.Name))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST", message = "Name is required" });
        }
        if (string.IsNullOrEmpty(request.Phase))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST", message = "Phase is required" });
        }
        if (string.IsNullOrEmpty(request.SystemPrompt))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST", message = "SystemPrompt is required" });
        }
        if (string.IsNullOrEmpty(request.UserPromptTemplate))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST", message = "UserPromptTemplate is required" });
        }

        // Check if template with same ID already exists
        var existing = await promptRepository.GetByIdAsync(request.Id, request.Phase, cancellationToken);
        if (existing is not null)
        {
            return Results.Conflict(new { error = "PROMPT_EXISTS", message = $"Prompt template '{request.Id}' already exists in phase '{request.Phase}'" });
        }

        var template = new PromptTemplate
        {
            Id = request.Id,
            Name = request.Name,
            Phase = request.Phase,
            Version = request.Version ?? 1,
            IsActive = false, // New templates start inactive
            SystemPrompt = request.SystemPrompt,
            UserPromptTemplate = request.UserPromptTemplate,
            Constraints = request.Constraints ?? new Dictionary<string, string>(),
            RequiredVariables = request.RequiredVariables ?? [],
            OptionalVariables = request.OptionalVariables ?? [],
            MaxTokens = request.MaxTokens,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        var created = await promptRepository.CreateAsync(template, cancellationToken);
        return Results.Created($"/api/prompts/{created.Id}?phase={created.Phase}", created);
    }

    /// <summary>
    /// Activates a prompt template.
    /// POST /api/prompts/{id}/activate
    /// </summary>
    private static async Task<IResult> ActivatePrompt(
        string id,
        IPromptRepository promptRepository,
        [FromQuery] string phase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE", message = "Phase query parameter is required" });
        }

        try
        {
            var activated = await promptRepository.ActivateAsync(id, phase, cancellationToken);
            return Results.Ok(activated);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = ex.Message });
        }
    }

    /// <summary>
    /// Deactivates a prompt template.
    /// POST /api/prompts/{id}/deactivate
    /// </summary>
    private static async Task<IResult> DeactivatePrompt(
        string id,
        IPromptRepository promptRepository,
        [FromQuery] string phase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE", message = "Phase query parameter is required" });
        }

        try
        {
            var deactivated = await promptRepository.DeactivateAsync(id, phase, cancellationToken);
            return Results.Ok(deactivated);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = ex.Message });
        }
    }
}

/// <summary>
/// Request model for creating a new prompt template.
/// </summary>
public record CreatePromptRequest
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The phase this prompt is used for.
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Version number.
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// The system prompt text.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// The user prompt template with Scriban variables.
    /// </summary>
    public required string UserPromptTemplate { get; init; }

    /// <summary>
    /// Additional constraints.
    /// </summary>
    public Dictionary<string, string>? Constraints { get; init; }

    /// <summary>
    /// Required variable names.
    /// </summary>
    public List<string>? RequiredVariables { get; init; }

    /// <summary>
    /// Optional variable names.
    /// </summary>
    public List<string>? OptionalVariables { get; init; }

    /// <summary>
    /// Maximum token budget.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Creator identity.
    /// </summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// Marker attribute for prompt admin endpoints.
/// When authentication is configured, these endpoints will require admin authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PromptAdminEndpointAttribute : Attribute
{
    /// <summary>
    /// The required role for accessing admin endpoints.
    /// </summary>
    public const string RequiredRole = "prompt-admin";
}
