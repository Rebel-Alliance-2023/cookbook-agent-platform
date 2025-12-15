using System.Text.Json;
using System.Text.Json.Nodes;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Prompts;
using Cookbook.Platform.Shared.Prompts.Templates;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Service for normalizing recipe data using LLM-generated JSON patches.
/// </summary>
public class NormalizeService : INormalizeService
{
    private readonly ILlmRouter _llmRouter;
    private readonly IPromptRenderer _promptRenderer;
    private readonly ILogger<NormalizeService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public NormalizeService(
        ILlmRouter llmRouter,
        IPromptRenderer promptRenderer,
        ILogger<NormalizeService> logger)
    {
        _llmRouter = llmRouter;
        _promptRenderer = promptRenderer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NormalizePatchResponse> GeneratePatchesAsync(
        Recipe recipe,
        IReadOnlyList<string>? focusAreas = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating normalize patches for recipe {RecipeId}", recipe.Id);

        // Build template variables
        var variables = new Dictionary<string, object?>
        {
            ["recipe"] = recipe
        };

        if (focusAreas != null && focusAreas.Count > 0)
        {
            variables["focus_areas"] = focusAreas;
        }

        // Render the prompt
        var userPrompt = _promptRenderer.Render(
            IngestPromptTemplates.NormalizeV1UserPromptTemplate,
            variables);

        // Create LLM request
        var request = new LlmRequest
        {
            SystemPrompt = IngestPromptTemplates.NormalizeV1SystemPrompt,
            Messages = [new LlmMessage { Role = "user", Content = userPrompt }],
            Temperature = 0.3,
            MaxTokens = 4096
        };

        // Call LLM
        var response = await _llmRouter.ChatAsync(request, cancellationToken);
        var content = response.Content?.Trim() ?? "";

        _logger.LogDebug("LLM response for normalize: {Length} chars", content.Length);

        // Parse response
        try
        {
            var patchResponse = JsonSerializer.Deserialize<NormalizePatchResponse>(content, JsonOptions);
            if (patchResponse == null)
            {
                _logger.LogWarning("Failed to parse normalize response, returning empty patches");
                return new NormalizePatchResponse
                {
                    Summary = "Failed to parse LLM response"
                };
            }

            _logger.LogInformation(
                "Generated {Count} normalize patches: {Low} low, {Medium} medium, {High} high risk",
                patchResponse.Patches.Count,
                patchResponse.LowRiskCount,
                patchResponse.MediumRiskCount,
                patchResponse.HighRiskCount);

            return patchResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse normalize response JSON");
            return new NormalizePatchResponse
            {
                Summary = $"Failed to parse LLM response: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public Task<NormalizePatchResult> ApplyPatchesAsync(
        Recipe recipe,
        IReadOnlyList<NormalizePatchOperation> patches,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying {Count} normalize patches to recipe {RecipeId}", 
            patches.Count, recipe.Id);

        if (patches.Count == 0)
        {
            return Task.FromResult(NormalizePatchResult.Succeeded(recipe, [], "No patches to apply"));
        }

        // Convert recipe to JSON for patching
        var recipeJson = JsonSerializer.Serialize(recipe, JsonOptions);
        var jsonNode = JsonNode.Parse(recipeJson);
        if (jsonNode == null)
        {
            return Task.FromResult(NormalizePatchResult.Failed("Failed to parse recipe as JSON"));
        }

        var appliedPatches = new List<NormalizePatchOperation>();
        var failedPatches = new List<NormalizePatchError>();

        foreach (var patch in patches)
        {
            try
            {
                ApplyPatch(jsonNode, patch);
                
                // Store original value in the applied patch
                var appliedPatch = patch with
                {
                    OriginalValue = GetValueAtPath(JsonNode.Parse(recipeJson)!, patch.Path)
                };
                appliedPatches.Add(appliedPatch);
                
                _logger.LogDebug("Applied patch: {Op} {Path}", patch.Op, patch.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply patch: {Op} {Path}", patch.Op, patch.Path);
                failedPatches.Add(new NormalizePatchError
                {
                    Patch = patch,
                    Error = ex.Message
                });
            }
        }

        // Deserialize back to Recipe
        Recipe? normalizedRecipe;
        try
        {
            normalizedRecipe = JsonSerializer.Deserialize<Recipe>(jsonNode.ToJsonString(), JsonOptions);
        }
        catch (JsonException ex)
        {
            return Task.FromResult(NormalizePatchResult.Failed($"Failed to deserialize patched recipe: {ex.Message}"));
        }

        if (normalizedRecipe == null)
        {
            return Task.FromResult(NormalizePatchResult.Failed("Patched recipe deserialized to null"));
        }

        var summary = $"Applied {appliedPatches.Count}/{patches.Count} patches";
        if (failedPatches.Count > 0)
        {
            summary += $", {failedPatches.Count} failed";
            return Task.FromResult(NormalizePatchResult.Partial(
                normalizedRecipe, appliedPatches, failedPatches, summary));
        }

        return Task.FromResult(NormalizePatchResult.Succeeded(normalizedRecipe, appliedPatches, summary));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ValidatePatches(Recipe recipe, IReadOnlyList<NormalizePatchOperation> patches)
    {
        var errors = new List<string>();

        foreach (var patch in patches)
        {
            // Validate path syntax
            if (string.IsNullOrWhiteSpace(patch.Path))
            {
                errors.Add($"Patch has empty path");
                continue;
            }

            if (!patch.Path.StartsWith('/'))
            {
                errors.Add($"Patch path must start with '/': {patch.Path}");
                continue;
            }

            // Validate operation
            if (patch.Op == JsonPatchOperationType.Replace || patch.Op == JsonPatchOperationType.Add)
            {
                // Value should not be null for replace/add (unless explicitly setting null)
            }

            // Validate path exists for replace/remove
            if (patch.Op == JsonPatchOperationType.Replace || patch.Op == JsonPatchOperationType.Remove)
            {
                var recipeJson = JsonSerializer.Serialize(recipe, JsonOptions);
                var jsonNode = JsonNode.Parse(recipeJson);
                if (jsonNode != null)
                {
                    try
                    {
                        var value = GetValueAtPath(jsonNode, patch.Path);
                        if (value == null && patch.Op == JsonPatchOperationType.Replace)
                        {
                            // Path doesn't exist, but might be OK for nested object creation
                        }
                    }
                    catch
                    {
                        errors.Add($"Path does not exist: {patch.Path}");
                    }
                }
            }
        }

        return errors;
    }

    private static void ApplyPatch(JsonNode node, NormalizePatchOperation patch)
    {
        var segments = ParsePath(patch.Path);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Empty path");
        }

        var current = node;
        
        // Navigate to parent
        for (int i = 0; i < segments.Length - 1; i++)
        {
            current = GetChild(current, segments[i]) 
                ?? throw new InvalidOperationException($"Path segment not found: {segments[i]}");
        }

        var lastSegment = segments[^1];

        switch (patch.Op)
        {
            case JsonPatchOperationType.Replace:
            case JsonPatchOperationType.Add:
                SetValue(current, lastSegment, patch.Value);
                break;
                
            case JsonPatchOperationType.Remove:
                RemoveValue(current, lastSegment);
                break;
                
            default:
                throw new InvalidOperationException($"Unknown operation: {patch.Op}");
        }
    }

    private static object? GetValueAtPath(JsonNode node, string path)
    {
        var segments = ParsePath(path);
        var current = node;

        foreach (var segment in segments)
        {
            current = GetChild(current, segment);
            if (current == null) return null;
        }

        return current.ToJsonString();
    }

    private static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return [];

        return path.TrimStart('/').Split('/')
            .Select(s => s.Replace("~1", "/").Replace("~0", "~"))
            .ToArray();
    }

    private static JsonNode? GetChild(JsonNode node, string segment)
    {
        if (node is JsonObject obj)
        {
            return obj[segment];
        }
        
        if (node is JsonArray arr && int.TryParse(segment, out var index))
        {
            return index >= 0 && index < arr.Count ? arr[index] : null;
        }

        return null;
    }

    private static void SetValue(JsonNode parent, string segment, object? value)
    {
        var jsonValue = value != null 
            ? JsonNode.Parse(JsonSerializer.Serialize(value, JsonOptions)) 
            : null;

        if (parent is JsonObject obj)
        {
            obj[segment] = jsonValue;
        }
        else if (parent is JsonArray arr && int.TryParse(segment, out var index))
        {
            if (index >= 0 && index < arr.Count)
            {
                arr[index] = jsonValue;
            }
            else if (index == arr.Count)
            {
                arr.Add(jsonValue);
            }
            else
            {
                throw new InvalidOperationException($"Array index out of bounds: {index}");
            }
        }
        else
        {
            throw new InvalidOperationException($"Cannot set value on node type: {parent.GetType().Name}");
        }
    }

    private static void RemoveValue(JsonNode parent, string segment)
    {
        if (parent is JsonObject obj)
        {
            obj.Remove(segment);
        }
        else if (parent is JsonArray arr && int.TryParse(segment, out var index))
        {
            if (index >= 0 && index < arr.Count)
            {
                arr.RemoveAt(index);
            }
        }
        else
        {
            throw new InvalidOperationException($"Cannot remove value from node type: {parent.GetType().Name}");
        }
    }
}
