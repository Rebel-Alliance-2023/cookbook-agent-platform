using System.Text.Json;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Defines the phases in the ingest pipeline.
/// </summary>
public static class IngestPhases
{
    public const string Fetch = "Ingest.Fetch";
    public const string Extract = "Ingest.Extract";
    public const string Validate = "Ingest.Validate";
    public const string ReviewReady = "Ingest.ReviewReady";
    
    /// <summary>
    /// Progress weights for each phase (redistributed without Discover phase).
    /// Total = 100%
    /// </summary>
    public static class Weights
    {
        public const int Fetch = 15;
        public const int Extract = 40;
        public const int Validate = 25;
        public const int ReviewReady = 10;
        public const int Finalize = 10; // Reserved for final state updates
    }
}

/// <summary>
/// Result of the ingest pipeline execution.
/// </summary>
public record IngestPipelineResult
{
    /// <summary>
    /// The recipe draft produced by the pipeline.
    /// </summary>
    public RecipeDraft? Draft { get; init; }
    
    /// <summary>
    /// Whether the pipeline completed successfully.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if the pipeline failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Error code for structured error handling.
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// The phase where the error occurred.
    /// </summary>
    public string? FailedPhase { get; init; }
}

/// <summary>
/// Context passed through the ingest pipeline phases.
/// </summary>
public class IngestPipelineContext
{
    /// <summary>
    /// The task being processed.
    /// </summary>
    public required AgentTask Task { get; init; }
    
    /// <summary>
    /// The parsed ingest payload.
    /// </summary>
    public required IngestPayload Payload { get; init; }
    
    /// <summary>
    /// Cancellation token for the pipeline.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
    
    /// <summary>
    /// Fetched HTML content (set by Fetch phase).
    /// </summary>
    public string? FetchedContent { get; set; }
    
    /// <summary>
    /// Content type of the fetched response.
    /// </summary>
    public string? ContentType { get; set; }
    
    /// <summary>
    /// HTTP status code from fetch.
    /// </summary>
    public int? HttpStatusCode { get; set; }
    
    /// <summary>
    /// Sanitized text content (set by Fetch phase).
    /// </summary>
    public string? SanitizedText { get; set; }
    
    /// <summary>
    /// Extracted JSON-LD recipe data if present.
    /// </summary>
    public string? JsonLdRecipe { get; set; }
    
    /// <summary>
    /// The extraction method used.
    /// </summary>
    public string? ExtractionMethod { get; set; }
    
    /// <summary>
    /// The final recipe draft.
    /// </summary>
    public RecipeDraft? Draft { get; set; }
    
    /// <summary>
    /// Artifact references stored during the pipeline.
    /// </summary>
    public List<ArtifactRef> Artifacts { get; } = [];
}

/// <summary>
/// Manages the execution of ingest phases for URL/Query recipe import.
/// </summary>
public class IngestPhaseRunner
{
    private readonly IMessagingBus _messagingBus;
    private readonly ILogger<IngestPhaseRunner> _logger;

    public IngestPhaseRunner(
        IMessagingBus messagingBus,
        ILogger<IngestPhaseRunner> logger)
    {
        _messagingBus = messagingBus;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full ingest pipeline: Fetch ? Extract ? Validate ? ReviewReady
    /// </summary>
    public async Task<IngestPipelineResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting ingest pipeline for task {TaskId}", task.TaskId);

        // Parse the ingest payload
        IngestPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<IngestPayload>(task.Payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize ingest payload");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse ingest payload for task {TaskId}", task.TaskId);
            return new IngestPipelineResult
            {
                Success = false,
                Error = "Invalid ingest payload format",
                ErrorCode = "INVALID_PAYLOAD",
                FailedPhase = "Initialization"
            };
        }

        var context = new IngestPipelineContext
        {
            Task = task,
            Payload = payload,
            CancellationToken = cancellationToken
        };

        try
        {
            // Phase 1: Fetch
            await ExecuteFetchPhaseAsync(context);
            
            // Phase 2: Extract
            await ExecuteExtractPhaseAsync(context);
            
            // Phase 3: Validate
            await ExecuteValidatePhaseAsync(context);
            
            // Phase 4: ReviewReady
            await ExecuteReviewReadyPhaseAsync(context);

            _logger.LogInformation("Ingest pipeline completed successfully for task {TaskId}", task.TaskId);

            return new IngestPipelineResult
            {
                Success = true,
                Draft = context.Draft
            };
        }
        catch (IngestPipelineException ex)
        {
            _logger.LogError(ex, "Ingest pipeline failed at phase {Phase} for task {TaskId}", 
                ex.Phase, task.TaskId);
            
            return new IngestPipelineResult
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = ex.ErrorCode,
                FailedPhase = ex.Phase
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ingest pipeline cancelled for task {TaskId}", task.TaskId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ingest pipeline for task {TaskId}", task.TaskId);
            
            return new IngestPipelineResult
            {
                Success = false,
                Error = ex.Message,
                ErrorCode = "INTERNAL_ERROR",
                FailedPhase = "Unknown"
            };
        }
    }

    /// <summary>
    /// Phase 1: Fetch content from URL.
    /// </summary>
    private async Task ExecuteFetchPhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing Fetch phase for task {TaskId}", context.Task.TaskId);
        
        await UpdateProgressAsync(context, IngestPhases.Fetch, 0, "Fetching URL content");

        // Validate we have a URL to fetch
        if (context.Payload.Mode == IngestMode.Url && string.IsNullOrWhiteSpace(context.Payload.Url))
        {
            throw new IngestPipelineException("URL is required for URL mode", "MISSING_URL", IngestPhases.Fetch);
        }

        // TODO: Implement actual fetch logic with IFetchService
        // For now, mark as placeholder
        await UpdateProgressAsync(context, IngestPhases.Fetch, IngestPhases.Weights.Fetch, "Fetch complete");
        
        _logger.LogInformation("Fetch phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Phase 2: Extract recipe data from content.
    /// </summary>
    private async Task ExecuteExtractPhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing Extract phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch;
        await UpdateProgressAsync(context, IngestPhases.Extract, baseProgress, "Extracting recipe data");

        // TODO: Implement JSON-LD extraction and LLM fallback
        // For now, mark as placeholder
        
        var finalProgress = baseProgress + IngestPhases.Weights.Extract;
        await UpdateProgressAsync(context, IngestPhases.Extract, finalProgress, "Extraction complete");
        
        _logger.LogInformation("Extract phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Phase 3: Validate extracted recipe data.
    /// </summary>
    private async Task ExecuteValidatePhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing Validate phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract;
        await UpdateProgressAsync(context, IngestPhases.Validate, baseProgress, "Validating recipe data");

        // TODO: Implement validation logic
        // For now, mark as placeholder
        
        var finalProgress = baseProgress + IngestPhases.Weights.Validate;
        await UpdateProgressAsync(context, IngestPhases.Validate, finalProgress, "Validation complete");
        
        _logger.LogInformation("Validate phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Phase 4: Prepare draft for review.
    /// </summary>
    private async Task ExecuteReviewReadyPhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing ReviewReady phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate;
        await UpdateProgressAsync(context, IngestPhases.ReviewReady, baseProgress, "Preparing draft for review");

        // Compute URL hash for the source
        var url = context.Payload.Url ?? "";
        var urlHash = Shared.Utilities.UrlHasher.ComputeHash(url);

        // TODO: Build final RecipeDraft with all artifacts
        // For now, create a placeholder draft
        context.Draft = new RecipeDraft
        {
            Recipe = new Shared.Models.Recipe
            {
                Id = null,
                Name = "Placeholder Recipe",
                Description = "Draft pending implementation"
            },
            Source = new RecipeSource
            {
                Url = url,
                UrlHash = urlHash,
                RetrievedAt = DateTime.UtcNow,
                ExtractionMethod = context.ExtractionMethod ?? "Pending"
            },
            ValidationReport = new ValidationReport
            {
                Errors = [],
                Warnings = ["Draft created with placeholder data"]
            },
            Artifacts = context.Artifacts
        };
        
        var finalProgress = baseProgress + IngestPhases.Weights.ReviewReady;
        await UpdateProgressAsync(context, IngestPhases.ReviewReady, finalProgress, "Draft ready for review");
        
        _logger.LogInformation("ReviewReady phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Updates task progress and publishes progress event.
    /// </summary>
    private async Task UpdateProgressAsync(IngestPipelineContext context, string phase, int progress, string message)
    {
        await _messagingBus.SetTaskStateAsync(context.Task.TaskId, new TaskState
        {
            TaskId = context.Task.TaskId,
            Status = Shared.Messaging.TaskStatus.Running,
            CurrentPhase = phase,
            Progress = Math.Min(progress, 100)
        }, cancellationToken: context.CancellationToken);

        await _messagingBus.PublishEventAsync(context.Task.ThreadId, new AgentEvent
        {
            EventId = Guid.NewGuid().ToString(),
            ThreadId = context.Task.ThreadId,
            EventType = "ingest.progress",
            Payload = JsonSerializer.Serialize(new
            {
                context.Task.TaskId,
                Phase = phase,
                Progress = progress,
                Message = message,
                Timestamp = DateTime.UtcNow
            })
        }, context.CancellationToken);
    }

    /// <summary>
    /// Calculates cumulative progress based on completed phases.
    /// </summary>
    public static int CalculateProgress(string currentPhase, int phaseProgress = 100)
    {
        return currentPhase switch
        {
            IngestPhases.Fetch => (int)(IngestPhases.Weights.Fetch * (phaseProgress / 100.0)),
            IngestPhases.Extract => IngestPhases.Weights.Fetch + (int)(IngestPhases.Weights.Extract * (phaseProgress / 100.0)),
            IngestPhases.Validate => IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + (int)(IngestPhases.Weights.Validate * (phaseProgress / 100.0)),
            IngestPhases.ReviewReady => IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate + (int)(IngestPhases.Weights.ReviewReady * (phaseProgress / 100.0)),
            _ => 0
        };
    }
}

/// <summary>
/// Exception thrown when an ingest pipeline phase fails.
/// </summary>
public class IngestPipelineException : Exception
{
    public string ErrorCode { get; }
    public string Phase { get; }

    public IngestPipelineException(string message, string errorCode, string phase)
        : base(message)
    {
        ErrorCode = errorCode;
        Phase = phase;
    }

    public IngestPipelineException(string message, string errorCode, string phase, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Phase = phase;
    }
}
