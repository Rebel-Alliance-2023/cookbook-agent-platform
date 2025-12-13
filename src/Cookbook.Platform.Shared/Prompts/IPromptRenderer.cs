namespace Cookbook.Platform.Shared.Prompts;

/// <summary>
/// Abstraction for prompt template rendering.
/// Templates use Scriban syntax for variable substitution.
/// </summary>
public interface IPromptRenderer
{
    /// <summary>
    /// Renders a template with the provided variables.
    /// </summary>
    /// <param name="template">The template string (Scriban syntax).</param>
    /// <param name="variables">Variable values to substitute.</param>
    /// <param name="requiredVariables">List of variable names that must be present.</param>
    /// <returns>The rendered prompt text.</returns>
    /// <exception cref="PromptRenderException">Thrown when required variables are missing or rendering fails.</exception>
    string Render(string template, IDictionary<string, object?> variables, IEnumerable<string>? requiredVariables = null);

    /// <summary>
    /// Renders a template with content truncation support.
    /// </summary>
    /// <param name="template">The template string (Scriban syntax).</param>
    /// <param name="variables">Variable values to substitute.</param>
    /// <param name="requiredVariables">List of variable names that must be present.</param>
    /// <param name="maxCharacters">Maximum character budget for the 'content' variable.</param>
    /// <returns>The rendered prompt text with content truncated if necessary.</returns>
    /// <exception cref="PromptRenderException">Thrown when required variables are missing or rendering fails.</exception>
    string RenderWithTruncation(
        string template,
        IDictionary<string, object?> variables,
        IEnumerable<string>? requiredVariables = null,
        int? maxCharacters = null);
}
