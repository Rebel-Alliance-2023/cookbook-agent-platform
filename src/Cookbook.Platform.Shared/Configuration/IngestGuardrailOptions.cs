namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Configuration options for verbatim content guardrails in the Recipe Ingest Agent.
/// These thresholds control when similarity warnings and errors are triggered.
/// Bind to the "Ingest:Guardrail" section in appsettings.json.
/// </summary>
public class IngestGuardrailOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Ingest:Guardrail";

    /// <summary>
    /// Token overlap count that triggers a warning.
    /// If the maximum contiguous token overlap exceeds this value, a warning is added.
    /// Default: 40 tokens.
    /// </summary>
    public int TokenOverlapWarningThreshold { get; set; } = 40;

    /// <summary>
    /// Token overlap count that triggers an error.
    /// If the maximum contiguous token overlap exceeds this value, an error is added.
    /// Default: 80 tokens.
    /// </summary>
    public int TokenOverlapErrorThreshold { get; set; } = 80;

    /// <summary>
    /// N-gram Jaccard similarity score that triggers a warning.
    /// Value between 0.0 and 1.0.
    /// Default: 0.20 (20% similarity).
    /// </summary>
    public double NgramSimilarityWarningThreshold { get; set; } = 0.20;

    /// <summary>
    /// N-gram Jaccard similarity score that triggers an error.
    /// Value between 0.0 and 1.0.
    /// Default: 0.35 (35% similarity).
    /// </summary>
    public double NgramSimilarityErrorThreshold { get; set; } = 0.35;

    /// <summary>
    /// The size of n-grams to use for similarity calculation.
    /// Default: 5 (5-grams).
    /// </summary>
    public int NgramSize { get; set; } = 5;

    /// <summary>
    /// Whether to automatically attempt to repair content that exceeds error thresholds.
    /// When true, the LLM will be prompted to paraphrase sections with high similarity.
    /// Default: true.
    /// </summary>
    public bool AutoRepairOnError { get; set; } = true;
}
