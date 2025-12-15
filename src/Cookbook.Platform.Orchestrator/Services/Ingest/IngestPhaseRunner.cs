using System.Diagnostics;
using System.Text.Json;
using Cookbook.Platform.Orchestrator.Metrics;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Defines the phases in the ingest pipeline.
/// </summary>
public static class IngestPhases
{
    public const string Fetch = "Ingest.Fetch";
    public const string Extract = "Ingest.Extract";
    public const string Validate = "Ingest.Validate";
    public const string RepairParaphrase = "Ingest.RepairParaphrase";
    public const string Normalize = "Ingest.Normalize";
    public const string ReviewReady = "Ingest.ReviewReady";
    
    /// <summary>
    /// Progress weights for each phase.
    /// Total = 100%
    /// </summary>
    public static class Weights
    {
        public const int Fetch = 15;
        public const int Extract = 35;
        public const int Validate = 20;
        public const int RepairParaphrase = 15;
        public const int ReviewReady = 10;
        public const int Finalize = 5; // Reserved for final state updates
    }
    
    /// <summary>
    /// Progress weights for Normalize mode (existing recipe).
    /// </summary>
    public static class NormalizeModeWeights
    {
        public const int FetchRecipe = 15;
        public const int Normalize = 55;
        public const int ReviewReady = 25;
        public const int Finalize = 5;
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
    /// Whether this is a Normalize mode task (normalize existing recipe).
    /// </summary>
    public bool IsNormalizeMode => Payload.Mode == IngestMode.Normalize;
    
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
    /// Similarity report from validation phase.
    /// </summary>
    public SimilarityReport? SimilarityReport { get; set; }
    
    #region Normalize Phase Results
    
    /// <summary>
    /// The recipe ID to normalize (for Normalize mode).
    /// </summary>
    public string? RecipeIdToNormalize { get; set; }
    
    /// <summary>
    /// The normalize patch response from LLM.
    /// </summary>
    public NormalizePatchResponse? NormalizePatchResponse { get; set; }
    
    /// <summary>
    /// The result of applying patches.
    /// </summary>
    public NormalizePatchResult? NormalizePatchResult { get; set; }
    
    #endregion
    
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
    private readonly ISimilarityDetector _similarityDetector;
    private readonly IRepairParaphraseService _repairService;
    private readonly INormalizeService _normalizeService;
    private readonly IArtifactStorageService _artifactStorage;
    private readonly IngestGuardrailOptions _guardrailOptions;
    private readonly IngestMetrics _metrics;
    private readonly ILogger<IngestPhaseRunner> _logger;

    public IngestPhaseRunner(
        IMessagingBus messagingBus,
        ISimilarityDetector similarityDetector,
        IRepairParaphraseService repairService,
        INormalizeService normalizeService,
        IArtifactStorageService artifactStorage,
        IOptions<IngestGuardrailOptions> guardrailOptions,
        IngestMetrics metrics,
        ILogger<IngestPhaseRunner> logger)
    {
        _messagingBus = messagingBus;
        _similarityDetector = similarityDetector;
        _repairService = repairService;
        _normalizeService = normalizeService;
        _artifactStorage = artifactStorage;
        _guardrailOptions = guardrailOptions.Value;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full ingest pipeline: Fetch ? Extract ? Validate ? RepairParaphrase ? ReviewReady
    /// </summary>
    public async Task<IngestPipelineResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var mode = "Url"; // Will be updated after payload parsing
        
        IngestLogEvents.PipelineStarted(_logger, task.TaskId, mode);

        // Parse the ingest payload
        IngestPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<IngestPayload>(task.Payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize ingest payload");
            
            // Update mode now that we have the payload
            mode = payload.Mode.ToString();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse ingest payload for task {TaskId}", task.TaskId);
            _metrics.RecordPhaseFailure("Initialization", "INVALID_PAYLOAD");
            return new IngestPipelineResult
            {
                Success = false,
                Error = "Invalid ingest payload format",
                ErrorCode = "INVALID_PAYLOAD",
                FailedPhase = "Initialization"
            };
        }
        
        // Record task created metric
        _metrics.RecordTaskCreated(mode);

        var context = new IngestPipelineContext
        {
            Task = task,
            Payload = payload,
            CancellationToken = cancellationToken
        };

        try
        {
            // Handle different modes
            if (context.IsNormalizeMode)
            {
                // Normalize mode pipeline: FetchRecipe ? Normalize ? ReviewReady
                await ExecuteNormalizePipelineAsync(context);
            }
            else
            {
                // URL mode pipeline
                
                // Phase 1: Fetch
                await ExecuteFetchPhaseAsync(context);
                
                // Phase 2: Extract
                await ExecuteExtractPhaseAsync(context);
                
                // Phase 3: Validate
                await ExecuteValidatePhaseAsync(context);
                
                // Phase 4: RepairParaphrase
                await ExecuteRepairParaphrasePhaseAsync(context);
                
                // Phase 5: ReviewReady
                await ExecuteReviewReadyPhaseAsync(context);
            }

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
        finally
        {
            pipelineStopwatch.Stop();
            _metrics.RecordPipelineDuration(pipelineStopwatch.Elapsed, mode);
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
    /// Phase 3: Validate extracted recipe data and run similarity checks.
    /// </summary>
    private async Task ExecuteValidatePhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing Validate phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract;
        await UpdateProgressAsync(context, IngestPhases.Validate, baseProgress, "Validating recipe data");

        var errors = new List<string>();
        var warnings = new List<string>();

        // Run similarity check if we have source content and extracted text
        if (!string.IsNullOrWhiteSpace(context.SanitizedText) && context.Draft?.Recipe != null)
        {
            await UpdateProgressAsync(context, IngestPhases.Validate, baseProgress + 5, "Running similarity analysis");
            
            await RunSimilarityCheckAsync(context, errors, warnings);
        }
        else
        {
            _logger.LogDebug("Skipping similarity check - no source text or recipe data available");
        }

        // TODO: Add additional validation rules here

        var finalProgress = baseProgress + IngestPhases.Weights.Validate;
        await UpdateProgressAsync(context, IngestPhases.Validate, finalProgress, "Validation complete");
        
        _logger.LogInformation("Validate phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Runs similarity detection comparing source content vs extracted Description/Instructions.
    /// </summary>
    private async Task RunSimilarityCheckAsync(
        IngestPipelineContext context, 
        List<string> errors, 
        List<string> warnings)
    {
        var sourceText = context.SanitizedText ?? "";
        var recipe = context.Draft?.Recipe;

        if (recipe == null)
        {
            _logger.LogWarning("Cannot run similarity check - no recipe in draft");
            return;
        }

        // Build sections to analyze: Description and Instructions
        var sections = new Dictionary<string, string>();
        
        if (!string.IsNullOrWhiteSpace(recipe.Description))
        {
            sections["Description"] = recipe.Description;
        }
        
        if (recipe.Instructions.Count > 0)
        {
            sections["Instructions"] = string.Join(" ", recipe.Instructions);
        }

        if (sections.Count == 0)
        {
            _logger.LogDebug("No sections to analyze for similarity");
            return;
        }

        try
        {
            _logger.LogInformation("Running similarity analysis for task {TaskId} with {SectionCount} sections",
                context.Task.TaskId, sections.Count);

            var report = await _similarityDetector.AnalyzeSectionsAsync(
                sourceText, 
                sections, 
                context.CancellationToken);

            context.SimilarityReport = report;

            // Add to validation warnings/errors based on policy violation
            if (report.ViolatesPolicy)
            {
                errors.Add($"High verbatim similarity detected: {report.MaxNgramSimilarity:P0} n-gram similarity, " +
                          $"{report.MaxContiguousTokenOverlap} contiguous token overlap. " +
                          "Content may violate copyright policy.");
                
                // CC-010: Record guardrail violation metric
                _metrics.RecordGuardrailViolation("similarity_error");
                IngestLogEvents.GuardrailViolation(_logger, context.Task.TaskId, "similarity_error",
                    $"N-gram: {report.MaxNgramSimilarity:P0}, Overlap: {report.MaxContiguousTokenOverlap}");
            }
            else if (report.MaxNgramSimilarity >= 0.5 || report.MaxContiguousTokenOverlap >= 25)
            {
                warnings.Add($"Moderate verbatim similarity detected: {report.MaxNgramSimilarity:P0} n-gram similarity. " +
                            "Consider reviewing for potential copyright concerns.");
                
                // CC-010: Record guardrail warning metric
                _metrics.RecordGuardrailViolation("similarity_warning");
                IngestLogEvents.GuardrailViolation(_logger, context.Task.TaskId, "similarity_warning",
                    $"N-gram: {report.MaxNgramSimilarity:P0}, Overlap: {report.MaxContiguousTokenOverlap}");
            }

            // Store similarity report as artifact
            await StoreSimilarityArtifactAsync(context, report);

            // CC-005: Structured logging for similarity check
            IngestLogEvents.SimilarityCheckResult(_logger, context.Task.TaskId, 
                report.MaxContiguousTokenOverlap, report.MaxNgramSimilarity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Similarity analysis failed for task {TaskId}", context.Task.TaskId);
            warnings.Add("Similarity analysis could not be completed. Manual review recommended.");
        }
    }

    /// <summary>
    /// Stores the similarity report as a JSON artifact.
    /// </summary>
    private async Task StoreSimilarityArtifactAsync(IngestPipelineContext context, SimilarityReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var artifact = await _artifactStorage.StoreAsync(
                context.Task.ThreadId,
                context.Task.TaskId,
                "similarity.json",
                json,
                "application/json",
                context.CancellationToken);

            context.Artifacts.Add(artifact);

            _logger.LogDebug("Stored similarity.json artifact for task {TaskId}", context.Task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store similarity artifact for task {TaskId}", context.Task.TaskId);
        }
    }

    /// <summary>
    /// Phase 4: Repair high-similarity content by paraphrasing if needed.
    /// </summary>
    private async Task ExecuteRepairParaphrasePhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing RepairParaphrase phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate;
        await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, baseProgress, "Checking if repair is needed");

        // Check if repair is needed
        var similarityReport = context.SimilarityReport;
        var shouldRepair = ShouldTriggerRepair(similarityReport);

        if (!shouldRepair)
        {
            _logger.LogInformation("No repair needed for task {TaskId}", context.Task.TaskId);
            var finalProgress = baseProgress + IngestPhases.Weights.RepairParaphrase;
            await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, finalProgress, "No repair needed");
            return;
        }

        // Check if AutoRepair is enabled
        if (!_guardrailOptions.AutoRepairOnError)
        {
            _logger.LogInformation("AutoRepair is disabled, skipping repair for task {TaskId}", context.Task.TaskId);
            var finalProgress = baseProgress + IngestPhases.Weights.RepairParaphrase;
            await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, finalProgress, "AutoRepair disabled");
            return;
        }

        await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, baseProgress + 5, "Calling LLM for paraphrasing");

        // Attempt repair
        if (context.Draft == null || string.IsNullOrWhiteSpace(context.SanitizedText))
        {
            _logger.LogWarning("Cannot repair - missing draft or source text for task {TaskId}", context.Task.TaskId);
            var finalProgress = baseProgress + IngestPhases.Weights.RepairParaphrase;
            await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, finalProgress, "Missing data for repair");
            return;
        }

        var repairResult = await _repairService.RepairAsync(
            context.Draft,
            context.SanitizedText,
            similarityReport!,
            context.CancellationToken);

        // Store repair artifact
        await StoreRepairArtifactAsync(context, repairResult);

        if (repairResult.Success && repairResult.RepairedDraft != null)
        {
            // Update context with repaired draft and new similarity report
            context.Draft = repairResult.RepairedDraft;
            context.SimilarityReport = repairResult.NewSimilarityReport;
            
            _logger.LogInformation("Repair successful for task {TaskId}. New similarity: {Similarity:P2}",
                context.Task.TaskId, repairResult.NewSimilarityReport?.MaxNgramSimilarity ?? 0);
        }
        else
        {
            _logger.LogWarning("Repair did not fully resolve similarity for task {TaskId}: {Error}",
                context.Task.TaskId, repairResult.Error ?? "Still violates policy");
        }

        var progress = baseProgress + IngestPhases.Weights.RepairParaphrase;
        await UpdateProgressAsync(context, IngestPhases.RepairParaphrase, progress, 
            repairResult.Success ? "Repair successful" : "Repair attempted but policy still violated");
        
        _logger.LogInformation("RepairParaphrase phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Determines if repair should be triggered based on similarity report.
    /// </summary>
    private bool ShouldTriggerRepair(SimilarityReport? report)
    {
        if (report == null)
            return false;

        // Trigger repair if policy is violated (error threshold exceeded)
        return report.ViolatesPolicy;
    }

    /// <summary>
    /// Stores the repair attempt result as an artifact.
    /// </summary>
    private async Task StoreRepairArtifactAsync(IngestPipelineContext context, RepairParaphraseResult result)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                result.Success,
                result.StillViolatesPolicy,
                result.Error,
                result.Details,
                NewSimilarity = result.NewSimilarityReport?.MaxNgramSimilarity,
                NewOverlap = result.NewSimilarityReport?.MaxContiguousTokenOverlap,
                LlmResponseLength = result.RawLlmResponse?.Length ?? 0
            }, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var artifact = await _artifactStorage.StoreAsync(
                context.Task.ThreadId,
                context.Task.TaskId,
                "repair.json",
                json,
                "application/json",
                context.CancellationToken);

            context.Artifacts.Add(artifact);

            _logger.LogDebug("Stored repair.json artifact for task {TaskId}", context.Task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store repair artifact for task {TaskId}", context.Task.TaskId);
        }
    }

    /// <summary>
    /// Phase 5: Prepare draft for review.
    /// </summary>
    private async Task ExecuteReviewReadyPhaseAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing ReviewReady phase for task {TaskId}", context.Task.TaskId);
        
        var baseProgress = IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate + IngestPhases.Weights.RepairParaphrase;
        await UpdateProgressAsync(context, IngestPhases.ReviewReady, baseProgress, "Preparing draft for review");

        // Compute URL hash for the source
        var url = context.Payload.Url ?? "";
        var urlHash = Shared.Utilities.UrlHasher.ComputeHash(url);

        // Build validation report including similarity-based errors/warnings
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();

        // Add similarity-based errors if policy violated
        if (context.SimilarityReport?.ViolatesPolicy == true)
        {
            validationErrors.Add($"High verbatim similarity detected: {context.SimilarityReport.MaxNgramSimilarity:P0} n-gram similarity, " +
                                $"{context.SimilarityReport.MaxContiguousTokenOverlap} contiguous token overlap.");
        }
        else if (context.SimilarityReport != null && 
                (context.SimilarityReport.MaxNgramSimilarity >= 0.5 || context.SimilarityReport.MaxContiguousTokenOverlap >= 25))
        {
            validationWarnings.Add($"Moderate verbatim similarity detected: {context.SimilarityReport.MaxNgramSimilarity:P0} n-gram similarity.");
        }

        // Add placeholder warning if draft is not yet fully implemented
        if (context.Draft == null)
        {
            validationWarnings.Add("Draft created with placeholder data");
        }

        // Build final RecipeDraft with SimilarityReport
        context.Draft = new RecipeDraft
        {
            Recipe = context.Draft?.Recipe ?? new Shared.Models.Recipe
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
                Errors = validationErrors,
                Warnings = validationWarnings
            },
            SimilarityReport = context.SimilarityReport,
            Artifacts = context.Artifacts
        };
        
        var finalProgress = baseProgress + IngestPhases.Weights.ReviewReady;
        await UpdateProgressAsync(context, IngestPhases.ReviewReady, finalProgress, "Draft ready for review");
        
        _logger.LogInformation("ReviewReady phase completed for task {TaskId}", context.Task.TaskId);
    }

    /// <summary>
    /// Executes the normalize pipeline for an existing recipe.
    /// Pipeline: FetchRecipe ? Normalize ? ReviewReady
    /// </summary>
    private async Task ExecuteNormalizePipelineAsync(IngestPipelineContext context)
    {
        _logger.LogInformation("Executing Normalize pipeline for task {TaskId}", context.Task.TaskId);

        // Get recipe ID from payload
        var recipeId = context.Payload.RecipeId;
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            throw new IngestPipelineException("Recipe ID is required for Normalize mode", "MISSING_RECIPE_ID", IngestPhases.Normalize);
        }
        
        context.RecipeIdToNormalize = recipeId;

        // Phase 1: Fetch existing recipe (placeholder - would fetch from Cosmos)
        await UpdateProgressAsync(context, IngestPhases.Normalize, 
            IngestPhases.NormalizeModeWeights.FetchRecipe, "Loading recipe from storage");
        
        // For now, create a placeholder recipe - actual implementation would fetch from storage
        var existingRecipe = new Shared.Models.Recipe
        {
            Id = recipeId,
            Name = "Recipe to normalize",
            Description = "Placeholder for recipe fetch"
        };

        // Phase 2: Generate normalize patches
        await UpdateProgressAsync(context, IngestPhases.Normalize, 
            IngestPhases.NormalizeModeWeights.FetchRecipe + 10, "Analyzing recipe for improvements");

        var focusAreas = context.Payload.NormalizeOptions?.FocusAreas;
        var patchResponse = await _normalizeService.GeneratePatchesAsync(
            existingRecipe, 
            focusAreas, 
            context.CancellationToken);

        context.NormalizePatchResponse = patchResponse;

        await UpdateProgressAsync(context, IngestPhases.Normalize, 
            IngestPhases.NormalizeModeWeights.FetchRecipe + 30, 
            $"Generated {patchResponse.Patches.Count} normalization suggestions");

        // Phase 3: Test-apply patches
        await UpdateProgressAsync(context, IngestPhases.Normalize, 
            IngestPhases.NormalizeModeWeights.FetchRecipe + 40, "Testing patch application");

        var patchResult = await _normalizeService.ApplyPatchesAsync(
            existingRecipe, 
            patchResponse.Patches.ToList(), 
            context.CancellationToken);

        context.NormalizePatchResult = patchResult;

        await UpdateProgressAsync(context, IngestPhases.Normalize, 
            IngestPhases.NormalizeModeWeights.FetchRecipe + IngestPhases.NormalizeModeWeights.Normalize, 
            patchResult.Summary);

        // Store normalize artifacts
        await StoreNormalizeArtifactsAsync(context);

        // Phase 4: Build draft for review
        await UpdateProgressAsync(context, IngestPhases.ReviewReady, 
            IngestPhases.NormalizeModeWeights.FetchRecipe + IngestPhases.NormalizeModeWeights.Normalize + 10, 
            "Preparing normalization for review");

        // Build validation report
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();

        if (!patchResult.Success)
        {
            foreach (var error in patchResult.FailedPatches)
            {
                validationErrors.Add($"Patch failed: {error.Patch.Path} - {error.Error}");
            }
        }

        if (patchResponse.HasHighRiskChanges)
        {
            validationWarnings.Add("Contains high-risk changes that require careful review");
        }

        context.Draft = new RecipeDraft
        {
            Recipe = patchResult.NormalizedRecipe ?? existingRecipe,
            Source = new RecipeSource
            {
                Url = $"normalize://{recipeId}",
                UrlHash = Shared.Utilities.UrlHasher.ComputeHash(recipeId),
                ExtractionMethod = "Normalize"
            },
            ValidationReport = new ValidationReport
            {
                Errors = validationErrors,
                Warnings = validationWarnings
            },
            NormalizePatchResponse = patchResponse,
            Artifacts = context.Artifacts
        };

        await UpdateProgressAsync(context, IngestPhases.ReviewReady, 100, "Normalization ready for review");
        
        _logger.LogInformation("Normalize pipeline completed for task {TaskId}: {PatchCount} patches", 
            context.Task.TaskId, patchResponse.Patches.Count);
    }

    /// <summary>
    /// Stores normalize phase artifacts.
    /// </summary>
    private async Task StoreNormalizeArtifactsAsync(IngestPipelineContext context)
    {
        if (context.NormalizePatchResponse == null)
            return;

        try
        {
            // Store normalize.patch.json
            var patchJson = JsonSerializer.Serialize(context.NormalizePatchResponse, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var patchArtifact = await _artifactStorage.StoreAsync(
                context.Task.ThreadId,
                context.Task.TaskId,
                "normalize.patch.json",
                patchJson,
                "application/json",
                context.CancellationToken);

            context.Artifacts.Add(patchArtifact);

            // Store normalize.diff.md (human-readable diff)
            var diffMd = GenerateNormalizeDiffMarkdown(context);
            
            var diffArtifact = await _artifactStorage.StoreAsync(
                context.Task.ThreadId,
                context.Task.TaskId,
                "normalize.diff.md",
                diffMd,
                "text/markdown",
                context.CancellationToken);

            context.Artifacts.Add(diffArtifact);

            _logger.LogDebug("Stored normalize artifacts for task {TaskId}", context.Task.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store normalize artifacts for task {TaskId}", context.Task.TaskId);
        }
    }

    /// <summary>
    /// Generates a human-readable markdown diff for normalize patches.
    /// </summary>
    private static string GenerateNormalizeDiffMarkdown(IngestPipelineContext context)
    {
        if (context.NormalizePatchResponse == null)
            return "# No patches generated";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Normalize Diff");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {context.NormalizePatchResponse.Summary}");
        sb.AppendLine();
        sb.AppendLine($"| Risk | Count |");
        sb.AppendLine("|------|-------|");
        sb.AppendLine($"| Low | {context.NormalizePatchResponse.LowRiskCount} |");
        sb.AppendLine($"| Medium | {context.NormalizePatchResponse.MediumRiskCount} |");
        sb.AppendLine($"| High | {context.NormalizePatchResponse.HighRiskCount} |");
        sb.AppendLine();

        foreach (var patch in context.NormalizePatchResponse.Patches)
        {
            var riskEmoji = patch.RiskCategory switch
            {
                NormalizePatchRiskCategory.Low => "??",
                NormalizePatchRiskCategory.Medium => "??",
                NormalizePatchRiskCategory.High => "??",
                _ => "?"
            };

            sb.AppendLine($"## {riskEmoji} {patch.Path}");
            sb.AppendLine();
            sb.AppendLine($"**Operation:** {patch.Op}");
            sb.AppendLine($"**Risk:** {patch.RiskCategory}");
            sb.AppendLine($"**Reason:** {patch.Reason}");
            
            if (patch.OriginalValue != null)
            {
                sb.AppendLine();
                sb.AppendLine("**Before:**");
                sb.AppendLine($"```");
                sb.AppendLine(patch.OriginalValue.ToString());
                sb.AppendLine($"```");
            }
            
            if (patch.Value != null)
            {
                sb.AppendLine();
                sb.AppendLine("**After:**");
                sb.AppendLine($"```");
                sb.AppendLine(patch.Value.ToString());
                sb.AppendLine($"```");
            }
            
            sb.AppendLine();
        }

        return sb.ToString();
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
            IngestPhases.RepairParaphrase => IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate + (int)(IngestPhases.Weights.RepairParaphrase * (phaseProgress / 100.0)),
            IngestPhases.ReviewReady => IngestPhases.Weights.Fetch + IngestPhases.Weights.Extract + IngestPhases.Weights.Validate + IngestPhases.Weights.RepairParaphrase + (int)(IngestPhases.Weights.ReviewReady * (phaseProgress / 100.0)),
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
