namespace Cookbook.Platform.Shared.Llm;

/// <summary>
/// Configuration options for LLM routing.
/// </summary>
public class LlmRouterOptions
{
    public const string SectionName = "Llm";

    /// <summary>
    /// Default LLM provider (OpenAI or Claude/Anthropic).
    /// </summary>
    public string DefaultProvider { get; set; } = "Claude";

    /// <summary>
    /// OpenAI provider configuration.
    /// </summary>
    public OpenAIOptions OpenAI { get; set; } = new();

    /// <summary>
    /// Anthropic/Claude provider configuration.
    /// </summary>
    public AnthropicOptions Anthropic { get; set; } = new();

    /// <summary>
    /// Phase-specific provider overrides.
    /// </summary>
    public Dictionary<string, string> PhaseProviders { get; set; } = new()
    {
        ["Research"] = "Claude",
        ["Analysis"] = "OpenAI"
    };
}

/// <summary>
/// OpenAI provider configuration.
/// </summary>
public class OpenAIOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o";
    public string? BaseUrl { get; set; }
}

/// <summary>
/// Anthropic/Claude provider configuration.
/// </summary>
public class AnthropicOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public string? BaseUrl { get; set; }
}
