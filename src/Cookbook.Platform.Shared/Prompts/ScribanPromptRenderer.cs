using System.Text;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;

namespace Cookbook.Platform.Shared.Prompts;

/// <summary>
/// Scriban-based implementation of prompt template rendering.
/// Supports variable substitution, required variable validation, and content truncation.
/// </summary>
public partial class ScribanPromptRenderer : IPromptRenderer
{
    /// <summary>
    /// Default character budget for content truncation.
    /// </summary>
    public const int DefaultContentCharacterBudget = 60_000;

    /// <summary>
    /// The variable name used for content that may need truncation.
    /// </summary>
    private const string ContentVariableName = "content";

    /// <inheritdoc />
    public string Render(
        string template,
        IDictionary<string, object?> variables,
        IEnumerable<string>? requiredVariables = null)
    {
        ValidateRequiredVariables(variables, requiredVariables);
        return RenderTemplate(template, variables);
    }

    /// <inheritdoc />
    public string RenderWithTruncation(
        string template,
        IDictionary<string, object?> variables,
        IEnumerable<string>? requiredVariables = null,
        int? maxCharacters = null)
    {
        ValidateRequiredVariables(variables, requiredVariables);

        // Apply truncation to content variable if present and budget specified
        var processedVariables = new Dictionary<string, object?>(variables);

        if (maxCharacters.HasValue &&
            processedVariables.TryGetValue(ContentVariableName, out var contentValue) &&
            contentValue is string content &&
            content.Length > maxCharacters.Value)
        {
            processedVariables[ContentVariableName] = TruncateContent(content, maxCharacters.Value);
        }

        return RenderTemplate(template, processedVariables);
    }

    /// <summary>
    /// Validates that all required variables are present in the provided variables dictionary.
    /// </summary>
    private static void ValidateRequiredVariables(
        IDictionary<string, object?> variables,
        IEnumerable<string>? requiredVariables)
    {
        if (requiredVariables == null)
        {
            return;
        }

        var missing = requiredVariables
            .Where(name => !variables.ContainsKey(name) || variables[name] == null)
            .ToList();

        if (missing.Count > 0)
        {
            throw PromptRenderException.MissingRequiredVariables(missing);
        }
    }

    /// <summary>
    /// Renders the template using Scriban.
    /// </summary>
    private static string RenderTemplate(string template, IDictionary<string, object?> variables)
    {
        try
        {
            var scribanTemplate = Template.Parse(template);

            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join("; ", scribanTemplate.Messages.Select(m => m.Message));
                throw new PromptRenderException($"Template parsing failed: {errors}");
            }

            var scriptObject = new ScriptObject();
            foreach (var (key, value) in variables)
            {
                scriptObject[key] = value ?? string.Empty;
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            return scribanTemplate.Render(context);
        }
        catch (PromptRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw PromptRenderException.RenderingFailed(template, ex);
        }
    }

    /// <summary>
    /// Truncates content to fit within the character budget using importance trimming.
    /// Prioritizes headings, ingredient lists, and instruction sections.
    /// </summary>
    /// <param name="content">The content to truncate.</param>
    /// <param name="maxCharacters">Maximum character budget.</param>
    /// <returns>Truncated content.</returns>
    public static string TruncateContent(string content, int maxCharacters)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxCharacters)
        {
            return content;
        }

        // Try importance-based trimming first
        var trimmed = TrimByImportance(content, maxCharacters);
        if (trimmed.Length <= maxCharacters)
        {
            return trimmed;
        }

        // Fall back to simple truncation with ellipsis
        return SimpleTrancate(content, maxCharacters);
    }

    /// <summary>
    /// Attempts to trim content by importance, preserving high-value sections.
    /// </summary>
    private static string TrimByImportance(string content, int maxCharacters)
    {
        var sections = ParseSections(content);

        if (sections.Count == 0)
        {
            return SimpleTrancate(content, maxCharacters);
        }

        // Score and sort sections by importance
        var scoredSections = sections
            .Select(s => (Section: s, Score: ScoreSection(s)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var result = new StringBuilder();
        var remainingBudget = maxCharacters - 50; // Reserve space for truncation indicator

        foreach (var (section, _) in scoredSections)
        {
            if (section.Length <= remainingBudget)
            {
                result.AppendLine(section);
                remainingBudget -= section.Length + Environment.NewLine.Length;
            }
            else if (remainingBudget > 100)
            {
                // Partial section inclusion
                result.AppendLine(section[..remainingBudget]);
                result.AppendLine("...[truncated]");
                break;
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Parses content into logical sections (split by headings or double newlines).
    /// </summary>
    private static List<string> ParseSections(string content)
    {
        // Split by markdown headings or double newlines
        var pattern = HeadingSplitPattern();
        var parts = pattern.Split(content)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // If no sections found, split by paragraphs
        if (parts.Count <= 1)
        {
            parts = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return parts;
    }

    /// <summary>
    /// Scores a section based on importance for recipe content.
    /// Higher scores indicate more important content to preserve.
    /// </summary>
    private static int ScoreSection(string section)
    {
        var score = 0;
        var lowerSection = section.ToLowerInvariant();

        // Highest priority: ingredients and instructions
        if (lowerSection.Contains("ingredient"))
        {
            score += 100;
        }

        if (lowerSection.Contains("instruction") || lowerSection.Contains("direction") || lowerSection.Contains("step"))
        {
            score += 90;
        }

        // High priority: recipe metadata
        if (lowerSection.Contains("recipe"))
        {
            score += 50;
        }

        // Medium priority: headings (markdown or HTML)
        if (section.TrimStart().StartsWith('#') || HeadingTagPattern().IsMatch(section))
        {
            score += 30;
        }

        // Medium priority: lists (likely ingredients or steps)
        if (ListItemPattern().IsMatch(section))
        {
            score += 40;
        }

        // Lower priority: long paragraphs without structure
        if (section.Length > 500 && !section.Contains('\n'))
        {
            score -= 20;
        }

        // Penalty for navigation/boilerplate patterns
        if (lowerSection.Contains("copyright") ||
            lowerSection.Contains("advertisement") ||
            lowerSection.Contains("subscribe") ||
            lowerSection.Contains("newsletter") ||
            lowerSection.Contains("cookie"))
        {
            score -= 50;
        }

        return score;
    }

    /// <summary>
    /// Simple truncation with ellipsis, trying to break at word boundaries.
    /// </summary>
    private static string SimpleTrancate(string content, int maxCharacters)
    {
        if (content.Length <= maxCharacters)
        {
            return content;
        }

        var truncateAt = maxCharacters - 20; // Reserve space for ellipsis message

        // Try to find a good break point (newline or space)
        var breakPoint = content.LastIndexOf('\n', truncateAt);
        if (breakPoint < truncateAt - 200)
        {
            breakPoint = content.LastIndexOf(' ', truncateAt);
        }

        if (breakPoint < truncateAt - 200 || breakPoint < 0)
        {
            breakPoint = truncateAt;
        }

        return content[..breakPoint].TrimEnd() + "\n\n...[content truncated]";
    }

    [GeneratedRegex(@"(?=^#{1,6}\s)", RegexOptions.Multiline)]
    private static partial Regex HeadingSplitPattern();

    [GeneratedRegex(@"<h[1-6]", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingTagPattern();

    [GeneratedRegex(@"^[\s]*[-*•]\s|^[\s]*\d+\.", RegexOptions.Multiline)]
    private static partial Regex ListItemPattern();
}
