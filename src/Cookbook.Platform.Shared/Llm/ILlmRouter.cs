namespace Cookbook.Platform.Shared.Llm;

/// <summary>
/// Abstraction for LLM routing with provider selection and retry/fallback.
/// </summary>
public interface ILlmRouter
{
    /// <summary>
    /// Sends a chat completion request to the appropriate provider.
    /// </summary>
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a chat completion response from the appropriate provider.
    /// </summary>
    IAsyncEnumerable<LlmStreamChunk> StreamChatAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an LLM chat request.
/// </summary>
public record LlmRequest
{
    public required List<LlmMessage> Messages { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
    public string? SystemPrompt { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents a message in an LLM conversation.
/// </summary>
public record LlmMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// Represents an LLM response.
/// </summary>
public record LlmResponse
{
    public required string Content { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public string? FinishReason { get; init; }
}

/// <summary>
/// Represents a streaming chunk from an LLM response.
/// </summary>
public record LlmStreamChunk
{
    public required string Content { get; init; }
    public bool IsComplete { get; init; }
    public string? FinishReason { get; init; }
}
