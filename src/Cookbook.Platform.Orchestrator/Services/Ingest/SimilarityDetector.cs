using System.Text.RegularExpressions;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Detects similarity between source content and extracted text.
/// Implements contiguous token overlap and n-gram Jaccard similarity analysis.
/// </summary>
public partial class SimilarityDetector : ISimilarityDetector
{
    private readonly ILogger<SimilarityDetector> _logger;
    private readonly SimilarityOptions _options;

    public SimilarityDetector(
        ILogger<SimilarityDetector> logger,
        IOptions<SimilarityOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<SimilarityReport> AnalyzeAsync(
        string sourceContent,
        string extractedText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceContent) || string.IsNullOrWhiteSpace(extractedText))
        {
            return Task.FromResult(new SimilarityReport
            {
                MaxContiguousTokenOverlap = 0,
                MaxNgramSimilarity = 0.0,
                ViolatesPolicy = false,
                Details = "Empty content provided for similarity analysis."
            });
        }

        var sourceTokens = Tokenize(sourceContent);
        var extractedTokens = Tokenize(extractedText);

        var maxOverlap = ComputeMaxContiguousOverlap(sourceTokens, extractedTokens);
        var ngramSimilarity = ComputeNgramJaccardSimilarity(sourceTokens, extractedTokens, _options.NgramSize);

        var violatesPolicy = maxOverlap >= _options.MaxContiguousOverlapThreshold ||
                             ngramSimilarity >= _options.MaxNgramSimilarityThreshold;

        var details = BuildDetails(maxOverlap, ngramSimilarity, violatesPolicy);

        _logger.LogDebug(
            "Similarity analysis: overlap={Overlap}, similarity={Similarity:P2}, violates={Violates}",
            maxOverlap, ngramSimilarity, violatesPolicy);

        return Task.FromResult(new SimilarityReport
        {
            MaxContiguousTokenOverlap = maxOverlap,
            MaxNgramSimilarity = ngramSimilarity,
            ViolatesPolicy = violatesPolicy,
            Details = details
        });
    }

    /// <inheritdoc />
    public async Task<SimilarityReport> AnalyzeSectionsAsync(
        string sourceContent,
        Dictionary<string, string> extractedSections,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceContent) || extractedSections.Count == 0)
        {
            return new SimilarityReport
            {
                MaxContiguousTokenOverlap = 0,
                MaxNgramSimilarity = 0.0,
                ViolatesPolicy = false,
                Details = "No sections provided for similarity analysis."
            };
        }

        var sourceTokens = Tokenize(sourceContent);
        var maxOverall = 0;
        var maxSimilarity = 0.0;
        var sectionDetails = new List<string>();

        foreach (var (sectionName, sectionText) in extractedSections)
        {
            if (string.IsNullOrWhiteSpace(sectionText))
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            var sectionTokens = Tokenize(sectionText);
            var overlap = ComputeMaxContiguousOverlap(sourceTokens, sectionTokens);
            var similarity = ComputeNgramJaccardSimilarity(sourceTokens, sectionTokens, _options.NgramSize);

            if (overlap > maxOverall)
                maxOverall = overlap;
            if (similarity > maxSimilarity)
                maxSimilarity = similarity;

            if (overlap >= _options.WarningOverlapThreshold || similarity >= _options.WarningNgramThreshold)
            {
                sectionDetails.Add($"{sectionName}: overlap={overlap}, similarity={similarity:P2}");
            }

            _logger.LogDebug(
                "Section '{Section}': overlap={Overlap}, similarity={Similarity:P2}",
                sectionName, overlap, similarity);
        }

        var violatesPolicy = maxOverall >= _options.MaxContiguousOverlapThreshold ||
                             maxSimilarity >= _options.MaxNgramSimilarityThreshold;

        var details = BuildSectionDetails(maxOverall, maxSimilarity, violatesPolicy, sectionDetails);

        return new SimilarityReport
        {
            MaxContiguousTokenOverlap = maxOverall,
            MaxNgramSimilarity = maxSimilarity,
            ViolatesPolicy = violatesPolicy,
            Details = details
        };
    }

    /// <inheritdoc />
    public string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Normalize text: lowercase, remove extra whitespace
        var normalized = text.ToLowerInvariant();
        
        // Extract words using regex - matches sequences of letters and numbers
        var matches = WordTokenRegex().Matches(normalized);
        
        return matches
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => w.Length >= _options.MinTokenLength)
            .ToArray();
    }

    /// <inheritdoc />
    public int ComputeMaxContiguousOverlap(string[] sourceTokens, string[] extractedTokens)
    {
        if (sourceTokens.Length == 0 || extractedTokens.Length == 0)
            return 0;

        // Build a set of source positions for each token for quick lookup
        var sourcePositions = new Dictionary<string, List<int>>();
        for (var i = 0; i < sourceTokens.Length; i++)
        {
            var token = sourceTokens[i];
            if (!sourcePositions.TryGetValue(token, out var positions))
            {
                positions = [];
                sourcePositions[token] = positions;
            }
            positions.Add(i);
        }

        var maxLength = 0;

        // For each starting position in extracted tokens, find matching runs in source
        for (var extractStart = 0; extractStart < extractedTokens.Length; extractStart++)
        {
            var firstToken = extractedTokens[extractStart];
            
            if (!sourcePositions.TryGetValue(firstToken, out var startPositions))
                continue;

            foreach (var sourceStart in startPositions)
            {
                // Count matching tokens from this position
                var length = 0;
                var extractIdx = extractStart;
                var sourceIdx = sourceStart;

                while (extractIdx < extractedTokens.Length &&
                       sourceIdx < sourceTokens.Length &&
                       extractedTokens[extractIdx] == sourceTokens[sourceIdx])
                {
                    length++;
                    extractIdx++;
                    sourceIdx++;
                }

                if (length > maxLength)
                    maxLength = length;
            }
        }

        return maxLength;
    }

    /// <inheritdoc />
    public double ComputeNgramJaccardSimilarity(string[] sourceTokens, string[] extractedTokens, int n = 5)
    {
        if (sourceTokens.Length < n || extractedTokens.Length < n)
            return 0.0;

        var sourceNgrams = GenerateNgrams(sourceTokens, n);
        var extractedNgrams = GenerateNgrams(extractedTokens, n);

        if (sourceNgrams.Count == 0 || extractedNgrams.Count == 0)
            return 0.0;

        var intersection = sourceNgrams.Intersect(extractedNgrams).Count();
        var union = sourceNgrams.Union(extractedNgrams).Count();

        if (union == 0)
            return 0.0;

        return (double)intersection / union;
    }

    /// <summary>
    /// Generates n-grams from a token array.
    /// </summary>
    private static HashSet<string> GenerateNgrams(string[] tokens, int n)
    {
        var ngrams = new HashSet<string>();
        
        for (var i = 0; i <= tokens.Length - n; i++)
        {
            var ngram = string.Join(" ", tokens, i, n);
            ngrams.Add(ngram);
        }

        return ngrams;
    }

    private string BuildDetails(int maxOverlap, double ngramSimilarity, bool violatesPolicy)
    {
        var status = violatesPolicy ? "VIOLATION" : "OK";
        return $"Status: {status}. " +
               $"Max contiguous overlap: {maxOverlap} tokens (threshold: {_options.MaxContiguousOverlapThreshold}). " +
               $"Max n-gram similarity: {ngramSimilarity:P2} (threshold: {_options.MaxNgramSimilarityThreshold:P2}).";
    }

    private string BuildSectionDetails(
        int maxOverlap,
        double maxSimilarity,
        bool violatesPolicy,
        List<string> sectionDetails)
    {
        var status = violatesPolicy ? "VIOLATION" : "OK";
        var details = $"Status: {status}. " +
                      $"Max contiguous overlap: {maxOverlap} tokens. " +
                      $"Max n-gram similarity: {maxSimilarity:P2}.";

        if (sectionDetails.Count > 0)
        {
            details += " High similarity sections: " + string.Join("; ", sectionDetails);
        }

        return details;
    }

    [GeneratedRegex(@"[\w]+", RegexOptions.Compiled)]
    private static partial Regex WordTokenRegex();
}

/// <summary>
/// Configuration options for similarity detection.
/// </summary>
public class SimilarityOptions
{
    /// <summary>
    /// Maximum allowed contiguous token overlap before triggering a policy violation.
    /// Default: 50 tokens.
    /// </summary>
    public int MaxContiguousOverlapThreshold { get; set; } = 50;

    /// <summary>
    /// Maximum allowed n-gram Jaccard similarity before triggering a policy violation.
    /// Default: 0.7 (70%).
    /// </summary>
    public double MaxNgramSimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// Overlap threshold for warning (but not violation).
    /// Default: 25 tokens.
    /// </summary>
    public int WarningOverlapThreshold { get; set; } = 25;

    /// <summary>
    /// N-gram similarity threshold for warning (but not violation).
    /// Default: 0.5 (50%).
    /// </summary>
    public double WarningNgramThreshold { get; set; } = 0.5;

    /// <summary>
    /// The size of n-grams for Jaccard similarity.
    /// Default: 5.
    /// </summary>
    public int NgramSize { get; set; } = 5;

    /// <summary>
    /// Minimum token length to include in analysis.
    /// Default: 2 (filters out single-letter words).
    /// </summary>
    public int MinTokenLength { get; set; } = 2;
}
