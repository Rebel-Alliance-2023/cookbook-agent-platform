using System.Collections.Concurrent;
using Cookbook.Platform.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Tracks per-domain failure state for circuit breaker pattern.
/// </summary>
public record DomainCircuitState
{
    /// <summary>
    /// The domain this state is for.
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState State { get; set; } = CircuitState.Closed;
    
    /// <summary>
    /// Timestamps of recent failures within the failure window.
    /// </summary>
    public ConcurrentQueue<DateTime> FailureTimestamps { get; } = new();
    
    /// <summary>
    /// When the circuit was opened (if open).
    /// </summary>
    public DateTime? OpenedAt { get; set; }
}

/// <summary>
/// Circuit breaker state.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, requests are allowed.
    /// </summary>
    Closed,
    
    /// <summary>
    /// Circuit is open, requests are blocked.
    /// </summary>
    Open
}

/// <summary>
/// Service for managing per-domain circuit breakers.
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Checks if requests to a domain are allowed.
    /// </summary>
    /// <param name="domain">The domain to check.</param>
    /// <returns>True if requests are allowed, false if circuit is open.</returns>
    bool IsAllowed(string domain);
    
    /// <summary>
    /// Records a successful request to a domain.
    /// </summary>
    /// <param name="domain">The domain that succeeded.</param>
    void RecordSuccess(string domain);
    
    /// <summary>
    /// Records a failed request to a domain.
    /// </summary>
    /// <param name="domain">The domain that failed.</param>
    void RecordFailure(string domain);
    
    /// <summary>
    /// Gets the current state of a domain's circuit.
    /// </summary>
    /// <param name="domain">The domain to check.</param>
    /// <returns>The current circuit state.</returns>
    DomainCircuitState? GetState(string domain);
    
    /// <summary>
    /// Resets the circuit for a domain (for testing/admin purposes).
    /// </summary>
    /// <param name="domain">The domain to reset.</param>
    void Reset(string domain);
}

/// <summary>
/// In-memory implementation of per-domain circuit breaker.
/// </summary>
public class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, DomainCircuitState> _circuits = new();
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreakerService> _logger;

    public CircuitBreakerService(
        IOptions<IngestOptions> ingestOptions,
        ILogger<CircuitBreakerService> logger)
    {
        _options = ingestOptions.Value.CircuitBreaker;
        _logger = logger;
    }

    /// <summary>
    /// Checks if requests to a domain are allowed.
    /// </summary>
    public bool IsAllowed(string domain)
    {
        var normalizedDomain = NormalizeDomain(domain);
        var state = GetOrCreateState(normalizedDomain);
        
        if (state.State == CircuitState.Closed)
        {
            return true;
        }
        
        // Check if block duration has elapsed
        if (state.OpenedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - state.OpenedAt.Value;
            if (elapsed >= _options.BlockDuration)
            {
                // Reset the circuit
                _logger.LogInformation(
                    "Circuit breaker for domain {Domain} has recovered after {Duration}",
                    normalizedDomain, elapsed);
                
                state.State = CircuitState.Closed;
                state.OpenedAt = null;
                
                // Clear old failures
                while (state.FailureTimestamps.TryDequeue(out _)) { }
                
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Records a successful request to a domain.
    /// </summary>
    public void RecordSuccess(string domain)
    {
        var normalizedDomain = NormalizeDomain(domain);
        
        // Success doesn't immediately reset the circuit, but we could
        // implement a half-open state here if needed
        _logger.LogDebug("Recorded success for domain {Domain}", normalizedDomain);
    }

    /// <summary>
    /// Records a failed request to a domain.
    /// </summary>
    public void RecordFailure(string domain)
    {
        var normalizedDomain = NormalizeDomain(domain);
        var state = GetOrCreateState(normalizedDomain);
        var now = DateTime.UtcNow;
        
        // Add the failure timestamp
        state.FailureTimestamps.Enqueue(now);
        
        // Remove failures outside the window
        var windowStart = now - _options.FailureWindow;
        while (state.FailureTimestamps.TryPeek(out var oldest) && oldest < windowStart)
        {
            state.FailureTimestamps.TryDequeue(out _);
        }
        
        // Check if we've hit the threshold
        var failureCount = state.FailureTimestamps.Count;
        
        _logger.LogDebug(
            "Recorded failure for domain {Domain}. Failures in window: {Count}/{Threshold}",
            normalizedDomain, failureCount, _options.FailureThreshold);
        
        if (failureCount >= _options.FailureThreshold && state.State == CircuitState.Closed)
        {
            state.State = CircuitState.Open;
            state.OpenedAt = now;
            
            _logger.LogWarning(
                "Circuit breaker OPENED for domain {Domain}. {Count} failures in {Window} minutes. Blocking for {Block} minutes.",
                normalizedDomain, failureCount, _options.FailureWindowMinutes, _options.BlockDurationMinutes);
        }
    }

    /// <summary>
    /// Gets the current state of a domain's circuit.
    /// </summary>
    public DomainCircuitState? GetState(string domain)
    {
        var normalizedDomain = NormalizeDomain(domain);
        return _circuits.TryGetValue(normalizedDomain, out var state) ? state : null;
    }

    /// <summary>
    /// Resets the circuit for a domain.
    /// </summary>
    public void Reset(string domain)
    {
        var normalizedDomain = NormalizeDomain(domain);
        _circuits.TryRemove(normalizedDomain, out _);
        _logger.LogInformation("Circuit breaker reset for domain {Domain}", normalizedDomain);
    }

    private DomainCircuitState GetOrCreateState(string domain)
    {
        return _circuits.GetOrAdd(domain, d => new DomainCircuitState { Domain = d });
    }

    private static string NormalizeDomain(string domain)
    {
        // Extract domain from URL if full URL is passed
        if (Uri.TryCreate(domain, UriKind.Absolute, out var uri))
        {
            return uri.Host.ToLowerInvariant();
        }
        
        return domain.ToLowerInvariant();
    }
}
