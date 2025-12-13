using System.Text.Json;
using System.Text.RegularExpressions;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Extracts recipes from plain text content using LLM.
/// </summary>
public partial class LlmRecipeExtractor : IRecipeExtractor
{
    private readonly ILlmRouter _llmRouter;
    private readonly IngestOptions _options;
    private readonly ILogger<LlmRecipeExtractor> _logger;

    private const int MaxRepairAttempts = 2;

    public ExtractionMethod Method => ExtractionMethod.Llm;

    /// <summary>
    /// Default extraction prompt template.
    /// </summary>
    private const string DefaultExtractionPrompt = """
        Extract the recipe from the following web page content and return it as a JSON object.
        
        The JSON must have this exact structure:
        {
            "name": "Recipe name (required)",
            "description": "Brief description of the dish",
            "prepTimeMinutes": 0,
            "cookTimeMinutes": 0,
            "servings": 4,
            "ingredients": [
                {"name": "ingredient name", "quantity": 1.0, "unit": "cup", "notes": "optional notes"}
            ],
            "instructions": [
                "Step 1 instruction",
                "Step 2 instruction"
            ],
            "cuisine": "Italian, Mexican, etc.",
            "dietType": "Vegetarian, Vegan, Gluten-Free, etc.",
            "tags": ["tag1", "tag2"],
            "imageUrl": "URL to recipe image if found"
        }
        
        Important:
        - Extract ALL ingredients and instructions from the content
        - Parse ingredient quantities as numbers (use decimals for fractions: 1/2 = 0.5)
        - Convert all times to minutes
        - If a value is not found, use null or omit the field
        - Return ONLY valid JSON, no markdown code blocks or extra text
        
        Web page content:
        {{content}}
        """;

    public LlmRecipeExtractor(
        ILlmRouter llmRouter,
        IOptions<IngestOptions> options,
        ILogger<LlmRecipeExtractor> logger)
    {
        _llmRouter = llmRouter;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// LLM extractor can always attempt extraction on text content.
    /// </summary>
    public bool CanExtract(string content)
    {
        return !string.IsNullOrWhiteSpace(content);
    }

    /// <summary>
    /// Extracts a recipe using LLM analysis.
    /// </summary>
    public async Task<ExtractionResult> ExtractAsync(string content, ExtractionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ExtractionResult.Failed("Content is empty", "EMPTY_CONTENT", ExtractionMethod.Llm);
        }

        // Truncate content to budget
        var truncatedContent = TruncateContent(content, context.ContentBudget);
        _logger.LogDebug("Truncated content from {Original} to {Truncated} chars", 
            content.Length, truncatedContent.Length);

        // Render prompt
        var prompt = RenderPrompt(context.PromptOverride ?? DefaultExtractionPrompt, truncatedContent);

        // Call LLM with repair loop
        var (recipe, repairAttempts, error) = await ExtractWithRepairAsync(prompt, cancellationToken);

        if (recipe == null)
        {
            return ExtractionResult.Failed(
                error ?? "Failed to extract recipe from content",
                "LLM_EXTRACTION_FAILED",
                ExtractionMethod.Llm) with { RepairAttempts = repairAttempts };
        }

        _logger.LogInformation("Successfully extracted recipe '{Name}' using LLM (repairs: {Repairs})", 
            recipe.Name, repairAttempts);

        return ExtractionResult.Succeeded(
            recipe,
            ExtractionMethod.Llm,
            confidence: repairAttempts == 0 ? 0.85 : 0.75,
            rawSource: truncatedContent) with { RepairAttempts = repairAttempts };
    }

    /// <summary>
    /// Truncates content to the specified character budget, preserving important sections.
    /// </summary>
    private string TruncateContent(string content, int budget)
    {
        if (content.Length <= budget)
            return content;

        // Simple truncation - in the future, could implement importance-based trimming
        // Prefer to cut from the end as recipe content is usually at the start
        return content[..budget];
    }

    /// <summary>
    /// Renders the prompt template with content.
    /// </summary>
    private static string RenderPrompt(string template, string content)
    {
        // Simple placeholder replacement - could use Scriban for more complex templates
        return template.Replace("{{content}}", content);
    }

    /// <summary>
    /// Calls the LLM and attempts to repair invalid JSON responses.
    /// </summary>
    private async Task<(Recipe? Recipe, int RepairAttempts, string? Error)> ExtractWithRepairAsync(
        string prompt, 
        CancellationToken cancellationToken)
    {
        string? lastError = null;
        string? lastResponse = null;

        for (int attempt = 0; attempt <= MaxRepairAttempts; attempt++)
        {
            try
            {
                string currentPrompt;
                
                if (attempt == 0)
                {
                    currentPrompt = prompt;
                }
                else
                {
                    // Repair prompt
                    currentPrompt = BuildRepairPrompt(lastResponse!, lastError!);
                    _logger.LogDebug("Attempting JSON repair (attempt {Attempt}/{Max})", attempt, MaxRepairAttempts);
                }

                var request = new LlmRequest
                {
                    Messages =
                    [
                        new LlmMessage { Role = "user", Content = currentPrompt }
                    ],
                    Temperature = 0.3, // Lower temperature for more consistent JSON
                    MaxTokens = 4096,
                    Metadata = new Dictionary<string, object>
                    {
                        ["purpose"] = "recipe_extraction",
                        ["attempt"] = attempt
                    }
                };

                var response = await _llmRouter.ChatAsync(request, cancellationToken);
                lastResponse = response.Content;

                // Try to parse the response
                var recipe = ParseLlmResponse(response.Content);
                if (recipe != null)
                {
                    return (recipe, attempt, null);
                }

                lastError = "Failed to parse response as Recipe";
            }
            catch (JsonException ex)
            {
                lastError = $"JSON parse error: {ex.Message}";
                _logger.LogWarning(ex, "JSON parse error on attempt {Attempt}", attempt);
            }
            catch (Exception ex)
            {
                lastError = $"LLM error: {ex.Message}";
                _logger.LogError(ex, "LLM error on attempt {Attempt}", attempt);
                
                // Don't retry on non-JSON errors
                if (ex is not JsonException)
                    break;
            }
        }

        return (null, MaxRepairAttempts, lastError);
    }

    /// <summary>
    /// Builds a repair prompt asking the LLM to fix the JSON.
    /// </summary>
    private static string BuildRepairPrompt(string previousResponse, string error)
    {
        return $"""
            Your previous response contained invalid JSON. Please fix it and return ONLY valid JSON.
            
            Error: {error}
            
            Previous response:
            {previousResponse}
            
            Return ONLY the corrected JSON object with the recipe data, no markdown or extra text.
            """;
    }

    /// <summary>
    /// Parses the LLM response into a Recipe.
    /// </summary>
    private Recipe? ParseLlmResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        // Clean up response - remove markdown code blocks if present
        var cleaned = CleanJsonResponse(response);

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return MapToRecipe(doc.RootElement);
        }
        catch (JsonException)
        {
            // Try to extract JSON from the response
            var jsonMatch = JsonObjectRegex().Match(response);
            if (jsonMatch.Success)
            {
                using var doc = JsonDocument.Parse(jsonMatch.Value);
                return MapToRecipe(doc.RootElement);
            }
            throw;
        }
    }

    /// <summary>
    /// Cleans markdown code blocks and other noise from JSON response.
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();
        
        // Remove markdown code blocks
        cleaned = Regex.Replace(cleaned, @"^```(?:json)?\s*", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\s*```$", "", RegexOptions.Multiline);
        
        return cleaned.Trim();
    }

    /// <summary>
    /// Maps a JSON element to a Recipe.
    /// </summary>
    private Recipe? MapToRecipe(JsonElement element)
    {
        var name = GetString(element, "name");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var id = $"draft-{Guid.NewGuid():N}";

        return new Recipe
        {
            Id = id,
            Name = name,
            Description = GetString(element, "description"),
            PrepTimeMinutes = GetInt(element, "prepTimeMinutes"),
            CookTimeMinutes = GetInt(element, "cookTimeMinutes"),
            Servings = GetInt(element, "servings", 4),
            Ingredients = ParseIngredients(element),
            Instructions = ParseInstructions(element),
            Cuisine = GetString(element, "cuisine"),
            DietType = GetString(element, "dietType"),
            Tags = ParseTags(element),
            ImageUrl = GetString(element, "imageUrl"),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && 
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
            
            if (prop.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.GetString(), out var value))
                return value;
        }
        return defaultValue;
    }

    private static List<Ingredient> ParseIngredients(JsonElement element)
    {
        var ingredients = new List<Ingredient>();

        if (!element.TryGetProperty("ingredients", out var prop) || 
            prop.ValueKind != JsonValueKind.Array)
            return ingredients;

        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var name = GetString(item, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    ingredients.Add(new Ingredient
                    {
                        Name = name,
                        Quantity = GetDouble(item, "quantity", 1),
                        Unit = GetString(item, "unit"),
                        Notes = GetString(item, "notes")
                    });
                }
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    ingredients.Add(new Ingredient { Name = str, Quantity = 1 });
                }
            }
        }

        return ingredients;
    }

    private static double GetDouble(JsonElement element, string propertyName, double defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetDouble();
            
            if (prop.ValueKind == JsonValueKind.String &&
                double.TryParse(prop.GetString(), out var value))
                return value;
        }
        return defaultValue;
    }

    private static List<string> ParseInstructions(JsonElement element)
    {
        var instructions = new List<string>();

        if (!element.TryGetProperty("instructions", out var prop))
            return instructions;

        if (prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var str = item.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                        instructions.Add(str);
                }
            }
        }
        else if (prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (!string.IsNullOrWhiteSpace(str))
            {
                instructions.AddRange(str.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return instructions;
    }

    private static List<string> ParseTags(JsonElement element)
    {
        var tags = new List<string>();

        if (!element.TryGetProperty("tags", out var prop) || 
            prop.ValueKind != JsonValueKind.Array)
            return tags;

        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    tags.Add(str);
            }
        }

        return tags;
    }

    [GeneratedRegex(@"\{[\s\S]*\}", RegexOptions.Compiled)]
    private static partial Regex JsonObjectRegex();
}
