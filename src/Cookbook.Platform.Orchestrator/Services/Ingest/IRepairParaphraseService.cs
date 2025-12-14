using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Service for repairing high-similarity content by paraphrasing.
/// </summary>
public interface IRepairParaphraseService
{
    /// <summary>
    /// Attempts to repair high-similarity content by prompting the LLM to paraphrase.
    /// </summary>
    /// <param name="draft">The recipe draft with high similarity content.</param>
    /// <param name="sourceText">The original source text for reference.</param>
    /// <param name="similarityReport">The similarity report indicating which sections have high similarity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A repair result with the updated draft and new similarity report.</returns>
    Task<RepairParaphraseResult> RepairAsync(
        RecipeDraft draft,
        string sourceText,
        SimilarityReport similarityReport,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a paraphrase repair operation.
/// </summary>
public record RepairParaphraseResult
{
    /// <summary>
    /// Whether the repair was successful (similarity reduced below error threshold).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The updated recipe draft with rephrased content.
    /// </summary>
    public RecipeDraft? RepairedDraft { get; init; }

    /// <summary>
    /// The new similarity report after repair.
    /// </summary>
    public SimilarityReport? NewSimilarityReport { get; init; }

    /// <summary>
    /// Whether the content still violates policy after repair.
    /// </summary>
    public bool StillViolatesPolicy { get; init; }

    /// <summary>
    /// Error message if repair failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Details about the repair attempt.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// The raw LLM response for artifact storage.
    /// </summary>
    public string? RawLlmResponse { get; init; }
}

/// <summary>
/// Section to be rephrased.
/// </summary>
public record SectionToRephrase
{
    public required string Name { get; init; }
    public required string OriginalText { get; init; }
    public double SimilarityScore { get; init; }
    public int TokenOverlap { get; init; }
}

/// <summary>
/// Rephrased section from LLM.
/// </summary>
public record RephrasedSection
{
    public required string Name { get; init; }
    public required string RephrasedText { get; init; }
}
