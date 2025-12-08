using System.ClientModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cookbook.Platform.Shared.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace Cookbook.Platform.Infrastructure.Llm;

/// <summary>
/// LLM router implementation with provider selection, retry, and fallback.
/// </summary>
public class LlmRouter : ILlmRouter
{
    private readonly LlmRouterOptions _options;
    private readonly ILogger<LlmRouter> _logger;
    private readonly ChatClient? _openAiClient;
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _resiliencePipeline;

    public LlmRouter(IOptions<LlmRouterOptions> options, ILogger<LlmRouter> logger, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        
        // Use dedicated LLM client with extended timeouts (no aggressive Polly defaults)
        _httpClient = httpClientFactory.CreateClient("LlmClient");

        // Log configuration for debugging
        _logger.LogInformation("LlmRouter initialized. DefaultProvider: {DefaultProvider}", _options.DefaultProvider);
        _logger.LogInformation("OpenAI configured: {OpenAIConfigured}, Model: {OpenAIModel}", 
            !string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey), _options.OpenAI.Model);
        _logger.LogInformation("Anthropic configured: {AnthropicConfigured}, Model: {AnthropicModel}", 
            !string.IsNullOrWhiteSpace(_options.Anthropic.ApiKey), _options.Anthropic.Model);

        // Initialize OpenAI client if configured
        if (!string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey))
        {
            var openAiClientOptions = new OpenAI.OpenAIClientOptions();
            if (!string.IsNullOrWhiteSpace(_options.OpenAI.BaseUrl))
            {
                openAiClientOptions.Endpoint = new Uri(_options.OpenAI.BaseUrl);
            }
            
            var openAiClient = new OpenAI.OpenAIClient(new ApiKeyCredential(_options.OpenAI.ApiKey), openAiClientOptions);
            _openAiClient = openAiClient.GetChatClient(_options.OpenAI.Model);
        }

        // Build resilience pipeline with decorrelated jitter backoff
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("LLM call failed, retrying attempt {Attempt}: {Exception}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var provider = DetermineProvider(request.Provider);
        
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => await ChatWithOpenAIAsync(request, ct),
                "claude" or "anthropic" => await ChatWithAnthropicAsync(request, ct),
                _ => throw new NotSupportedException($"Provider '{provider}' is not supported")
            };
        }, cancellationToken);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamChatAsync(LlmRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var provider = DetermineProvider(request.Provider);

        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            await foreach (var chunk in StreamWithOpenAIAsync(request, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            // For Anthropic, fall back to non-streaming
            var response = await ChatWithAnthropicAsync(request, cancellationToken);
            yield return new LlmStreamChunk
            {
                Content = response.Content,
                IsComplete = true,
                FinishReason = response.FinishReason
            };
        }
    }

    private string DetermineProvider(string? requestedProvider)
    {
        if (!string.IsNullOrWhiteSpace(requestedProvider))
        {
            return requestedProvider;
        }

        return _options.DefaultProvider;
    }

    private async Task<LlmResponse> ChatWithOpenAIAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (_openAiClient == null)
        {
            throw new InvalidOperationException("OpenAI client is not configured");
        }

        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));
        }

        foreach (var msg in request.Messages)
        {
            messages.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => ChatMessage.CreateUserMessage(msg.Content),
                "assistant" => ChatMessage.CreateAssistantMessage(msg.Content),
                "system" => ChatMessage.CreateSystemMessage(msg.Content),
                _ => ChatMessage.CreateUserMessage(msg.Content)
            });
        }

        var options = new ChatCompletionOptions
        {
            Temperature = (float)request.Temperature,
            MaxOutputTokenCount = request.MaxTokens
        };

        var response = await _openAiClient.CompleteChatAsync(messages, options, cancellationToken);
        var completion = response.Value;

        return new LlmResponse
        {
            Content = completion.Content[0].Text ?? string.Empty,
            Provider = "OpenAI",
            Model = _options.OpenAI.Model,
            PromptTokens = completion.Usage?.InputTokenCount ?? 0,
            CompletionTokens = completion.Usage?.OutputTokenCount ?? 0,
            FinishReason = completion.FinishReason.ToString()
        };
    }

    private async Task<LlmResponse> ChatWithAnthropicAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Anthropic.ApiKey))
        {
            throw new InvalidOperationException("Anthropic API key is not configured");
        }

        _logger.LogInformation("Calling Anthropic API with model: {Model}", _options.Anthropic.Model);

        var anthropicMessages = request.Messages.Select(m => new
        {
            role = m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
            content = m.Content
        }).ToList();

        var requestBody = new
        {
            model = _options.Anthropic.Model,
            max_tokens = request.MaxTokens,
            system = request.SystemPrompt ?? "You are a helpful cooking assistant.",
            messages = anthropicMessages
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        
        httpRequest.Headers.Add("x-api-key", _options.Anthropic.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Anthropic API error. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return new LlmResponse
        {
            Content = anthropicResponse?.Content?.FirstOrDefault()?.Text ?? string.Empty,
            Provider = "Anthropic",
            Model = _options.Anthropic.Model,
            PromptTokens = anthropicResponse?.Usage?.InputTokens ?? 0,
            CompletionTokens = anthropicResponse?.Usage?.OutputTokens ?? 0,
            FinishReason = anthropicResponse?.StopReason ?? "stop"
        };
    }

    private async IAsyncEnumerable<LlmStreamChunk> StreamWithOpenAIAsync(LlmRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_openAiClient == null)
        {
            throw new InvalidOperationException("OpenAI client is not configured");
        }

        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));
        }

        foreach (var msg in request.Messages)
        {
            messages.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => ChatMessage.CreateUserMessage(msg.Content),
                "assistant" => ChatMessage.CreateAssistantMessage(msg.Content),
                "system" => ChatMessage.CreateSystemMessage(msg.Content),
                _ => ChatMessage.CreateUserMessage(msg.Content)
            });
        }

        var options = new ChatCompletionOptions
        {
            Temperature = (float)request.Temperature,
            MaxOutputTokenCount = request.MaxTokens
        };

        await foreach (var update in _openAiClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                yield return new LlmStreamChunk
                {
                    Content = part.Text ?? string.Empty,
                    IsComplete = update.FinishReason.HasValue,
                    FinishReason = update.FinishReason?.ToString()
                };
            }
        }
    }

    // Anthropic response models
    private record AnthropicResponse
    {
        public List<AnthropicContent>? Content { get; init; }
        public AnthropicUsage? Usage { get; init; }
        public string? StopReason { get; init; }
    }

    private record AnthropicContent
    {
        public string? Type { get; init; }
        public string? Text { get; init; }
    }

    private record AnthropicUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
    }
}
