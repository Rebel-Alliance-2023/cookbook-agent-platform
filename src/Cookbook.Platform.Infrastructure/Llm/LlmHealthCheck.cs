using Cookbook.Platform.Shared.Llm;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Infrastructure.Llm;

/// <summary>
/// Health check that validates LLM provider configuration and connectivity.
/// Results appear in Aspire Dashboard.
/// </summary>
public class LlmHealthCheck : IHealthCheck
{
    private readonly LlmRouterOptions _options;
    private readonly ILogger<LlmHealthCheck> _logger;

    public LlmHealthCheck(IOptions<LlmRouterOptions> options, ILogger<LlmHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var data = new Dictionary<string, object>();

        // Check OpenAI configuration
        var openAiConfigured = !string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey);
        data["OpenAI.Configured"] = openAiConfigured;
        data["OpenAI.Model"] = _options.OpenAI.Model;
        
        if (!openAiConfigured)
        {
            issues.Add("OpenAI API key is not configured");
        }

        // Check Anthropic configuration
        var anthropicConfigured = !string.IsNullOrWhiteSpace(_options.Anthropic.ApiKey);
        data["Anthropic.Configured"] = anthropicConfigured;
        data["Anthropic.Model"] = _options.Anthropic.Model;
        
        if (!anthropicConfigured)
        {
            issues.Add("Anthropic API key is not configured");
        }

        // Check default provider
        data["DefaultProvider"] = _options.DefaultProvider;

        // Determine health status
        var defaultProvider = _options.DefaultProvider.ToLowerInvariant();
        
        if (defaultProvider == "openai" && !openAiConfigured)
        {
            _logger.LogError("LLM Health Check failed: Default provider is OpenAI but API key is missing");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Default LLM provider (OpenAI) is not configured", 
                data: data));
        }

        if ((defaultProvider == "claude" || defaultProvider == "anthropic") && !anthropicConfigured)
        {
            _logger.LogError("LLM Health Check failed: Default provider is Anthropic but API key is missing");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Default LLM provider (Anthropic) is not configured", 
                data: data));
        }

        if (issues.Count > 0)
        {
            _logger.LogWarning("LLM Health Check degraded: {Issues}", string.Join(", ", issues));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Some LLM providers not configured: {string.Join(", ", issues)}", 
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All LLM providers configured", data: data));
    }
}
