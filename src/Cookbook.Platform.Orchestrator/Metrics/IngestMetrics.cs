using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cookbook.Platform.Orchestrator.Metrics;

/// <summary>
/// Metrics instrumentation for the Recipe Ingest Agent.
/// Uses .NET System.Diagnostics.Metrics for observability.
/// </summary>
public class IngestMetrics
{
    /// <summary>
    /// Meter name for the ingest agent metrics.
    /// </summary>
    public const string MeterName = "Cookbook.Ingest";

    private readonly Counter<long> _tasksCreated;
    private readonly Counter<long> _phaseFailures;
    private readonly Counter<long> _extractionMethods;
    private readonly Counter<long> _guardrailViolations;
    private readonly Counter<long> _circuitBreakerTrips;
    private readonly Histogram<double> _phaseDuration;
    private readonly Histogram<double> _pipelineDuration;

    public IngestMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // CC-006: Tasks created counter
        _tasksCreated = meter.CreateCounter<long>(
            "ingest.tasks.created",
            unit: "{task}",
            description: "Total number of ingest tasks created");

        // CC-008: Phase failures counter
        _phaseFailures = meter.CreateCounter<long>(
            "ingest.phase.failures",
            unit: "{failure}",
            description: "Number of phase execution failures");

        // CC-009: Extraction method counter
        _extractionMethods = meter.CreateCounter<long>(
            "ingest.extraction.method",
            unit: "{extraction}",
            description: "Extraction method used (json-ld, llm)");

        // CC-010: Guardrail violations counter
        _guardrailViolations = meter.CreateCounter<long>(
            "ingest.guardrail.violations",
            unit: "{violation}",
            description: "Number of guardrail violations detected");

        // CC-011: Circuit breaker trips counter
        _circuitBreakerTrips = meter.CreateCounter<long>(
            "ingest.circuitbreaker.trips",
            unit: "{trip}",
            description: "Number of circuit breaker trips");

        // CC-007: Phase duration histogram
        _phaseDuration = meter.CreateHistogram<double>(
            "ingest.phase.duration",
            unit: "s",
            description: "Duration of each pipeline phase in seconds");

        // Pipeline duration histogram
        _pipelineDuration = meter.CreateHistogram<double>(
            "ingest.pipeline.duration",
            unit: "s",
            description: "Total duration of the ingest pipeline in seconds");
    }

    /// <summary>
    /// Records that a new ingest task was created.
    /// </summary>
    /// <param name="mode">The ingest mode (Url, Query, Normalize)</param>
    public void RecordTaskCreated(string mode)
    {
        _tasksCreated.Add(1, new KeyValuePair<string, object?>("mode", mode));
    }

    /// <summary>
    /// Records a phase failure.
    /// </summary>
    /// <param name="phase">The phase that failed (e.g., Fetch, Extract, Validate)</param>
    /// <param name="errorCode">The error code</param>
    public void RecordPhaseFailure(string phase, string? errorCode = null)
    {
        _phaseFailures.Add(1,
            new KeyValuePair<string, object?>("phase", phase),
            new KeyValuePair<string, object?>("error_code", errorCode ?? "UNKNOWN"));
    }

    /// <summary>
    /// Records the extraction method used.
    /// </summary>
    /// <param name="method">The extraction method (json-ld, llm)</param>
    public void RecordExtractionMethod(string method)
    {
        _extractionMethods.Add(1, new KeyValuePair<string, object?>("method", method));
    }

    /// <summary>
    /// Records a guardrail violation.
    /// </summary>
    /// <param name="violationType">Type of violation (similarity_warning, similarity_error)</param>
    public void RecordGuardrailViolation(string violationType)
    {
        _guardrailViolations.Add(1, new KeyValuePair<string, object?>("type", violationType));
    }

    /// <summary>
    /// Records a circuit breaker trip.
    /// </summary>
    /// <param name="host">The host that triggered the circuit breaker</param>
    public void RecordCircuitBreakerTrip(string host)
    {
        _circuitBreakerTrips.Add(1, new KeyValuePair<string, object?>("host", host));
    }

    /// <summary>
    /// Records the duration of a pipeline phase.
    /// </summary>
    /// <param name="phase">The phase name</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    /// <param name="success">Whether the phase succeeded</param>
    public void RecordPhaseDuration(string phase, double durationSeconds, bool success)
    {
        _phaseDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("phase", phase),
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// Records the total duration of a pipeline execution.
    /// </summary>
    /// <param name="duration">The total duration</param>
    /// <param name="mode">The ingest mode</param>
    public void RecordPipelineDuration(TimeSpan duration, string mode)
    {
        _pipelineDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("mode", mode));
    }

    /// <summary>
    /// Creates a stopwatch for timing a phase. Dispose to record the duration.
    /// </summary>
    /// <param name="phaseName">Name of the phase being timed</param>
    /// <returns>A disposable phase timer</returns>
    public PhaseTimer StartPhaseTimer(string phaseName)
    {
        return new PhaseTimer(this, phaseName);
    }
}

/// <summary>
/// Timer for measuring phase duration. Dispose to record the measurement.
/// </summary>
public sealed class PhaseTimer : IDisposable
{
    private readonly IngestMetrics _metrics;
    private readonly string _phaseName;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;
    private bool _success = true;

    internal PhaseTimer(IngestMetrics metrics, string phaseName)
    {
        _metrics = metrics;
        _phaseName = phaseName;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Marks the phase as failed.
    /// </summary>
    public void MarkFailed()
    {
        _success = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _stopwatch.Stop();
        _metrics.RecordPhaseDuration(_phaseName, _stopwatch.Elapsed.TotalSeconds, _success);
    }
}

/// <summary>
/// Structured logging events for the ingest pipeline.
/// Uses high-performance logging with source generation.
/// </summary>
public static partial class IngestLogEvents
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Starting ingest pipeline for task {TaskId} in mode {Mode}")]
    public static partial void PipelineStarted(ILogger logger, string taskId, string mode);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Pipeline completed for task {TaskId} in {DurationMs}ms with status {Status}")]
    public static partial void PipelineCompleted(ILogger logger, string taskId, long durationMs, string status);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Starting phase {Phase} for task {TaskId}")]
    public static partial void PhaseStarted(ILogger logger, string phase, string taskId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Phase {Phase} completed for task {TaskId} in {DurationMs}ms")]
    public static partial void PhaseCompleted(ILogger logger, string phase, string taskId, long durationMs);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "Phase {Phase} failed for task {TaskId} with error {ErrorCode}: {ErrorMessage}")]
    public static partial void PhaseFailed(ILogger logger, string phase, string taskId, string errorCode, string errorMessage);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Recipe extracted from {Url} using method {ExtractionMethod}")]
    public static partial void RecipeExtracted(ILogger logger, string url, string extractionMethod);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Warning,
        Message = "Guardrail violation detected for task {TaskId}: {ViolationType} - {Details}")]
    public static partial void GuardrailViolation(ILogger logger, string taskId, string violationType, string details);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Warning,
        Message = "Circuit breaker tripped for host {Host}")]
    public static partial void CircuitBreakerTripped(ILogger logger, string host);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Similarity check for task {TaskId}: overlap={Overlap}%, similarity={Similarity:F2}")]
    public static partial void SimilarityCheckResult(ILogger logger, string taskId, int overlap, double similarity);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Repair paraphrase triggered for task {TaskId}, attempt {Attempt}")]
    public static partial void RepairParaphraseTriggered(ILogger logger, string taskId, int attempt);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Normalize phase for task {TaskId}: {PatchCount} patches generated")]
    public static partial void NormalizePatchesGenerated(ILogger logger, string taskId, int patchCount);
}
