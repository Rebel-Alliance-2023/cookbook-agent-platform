namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for the circuit breaker used in HTTP fetch operations.
/// The circuit breaker prevents repeated requests to failing domains.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// The number of failures within the failure window that triggers the circuit breaker.
    /// Default: 5 failures.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// The time window (in minutes) during which failures are counted.
    /// Default: 10 minutes.
    /// </summary>
    public int FailureWindowMinutes { get; set; } = 10;

    /// <summary>
    /// The duration (in minutes) for which requests to a domain are blocked after the circuit trips.
    /// Default: 30 minutes.
    /// </summary>
    public int BlockDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets the failure window as a TimeSpan.
    /// </summary>
    public TimeSpan FailureWindow => TimeSpan.FromMinutes(FailureWindowMinutes);

    /// <summary>
    /// Gets the block duration as a TimeSpan.
    /// </summary>
    public TimeSpan BlockDuration => TimeSpan.FromMinutes(BlockDurationMinutes);
}
