namespace Cookbook.Platform.Shared.Prompts;

/// <summary>
/// Exception thrown when prompt template rendering fails.
/// </summary>
public class PromptRenderException : Exception
{
    /// <summary>
    /// The variable names that were missing during rendering.
    /// </summary>
    public IReadOnlyList<string> MissingVariables { get; }

    /// <summary>
    /// The template that failed to render.
    /// </summary>
    public string? Template { get; }

    /// <summary>
    /// Initializes a new instance of the PromptRenderException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PromptRenderException(string message)
        : base(message)
    {
        MissingVariables = [];
    }

    /// <summary>
    /// Initializes a new instance of the PromptRenderException class with missing variables.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="missingVariables">The names of missing required variables.</param>
    public PromptRenderException(string message, IEnumerable<string> missingVariables)
        : base(message)
    {
        MissingVariables = missingVariables.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the PromptRenderException class with template context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="template">The template that failed to render.</param>
    /// <param name="innerException">The inner exception.</param>
    public PromptRenderException(string message, string template, Exception innerException)
        : base(message, innerException)
    {
        Template = template;
        MissingVariables = [];
    }

    /// <summary>
    /// Initializes a new instance of the PromptRenderException class for missing variables.
    /// </summary>
    /// <param name="missingVariables">The names of missing required variables.</param>
    public static PromptRenderException MissingRequiredVariables(IEnumerable<string> missingVariables)
    {
        var variables = missingVariables.ToList();
        var message = variables.Count == 1
            ? $"Required variable '{variables[0]}' is missing."
            : $"Required variables are missing: {string.Join(", ", variables.Select(v => $"'{v}'"))}";

        return new PromptRenderException(message, variables);
    }

    /// <summary>
    /// Initializes a new instance of the PromptRenderException class for rendering errors.
    /// </summary>
    /// <param name="template">The template that failed to render.</param>
    /// <param name="innerException">The inner exception from the template engine.</param>
    public static PromptRenderException RenderingFailed(string template, Exception innerException)
    {
        return new PromptRenderException(
            $"Failed to render prompt template: {innerException.Message}",
            template,
            innerException);
    }
}
