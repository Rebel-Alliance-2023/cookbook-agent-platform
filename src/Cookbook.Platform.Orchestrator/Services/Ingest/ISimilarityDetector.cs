using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Interface for detecting similarity between source content and extracted text.
/// Used for verbatim content guardrails to prevent copyright issues.
/// </summary>
public interface ISimilarityDetector
{
    /// <summary>
    /// Analyzes similarity between source content and extracted text.
    /// </summary>
    /// <param name="sourceContent">The original source content (e.g., fetched HTML/text).</param>
    /// <param name="extractedText">The extracted text to compare (e.g., description, instructions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A SimilarityReport containing overlap metrics and policy violation status.</returns>
    Task<SimilarityReport> AnalyzeAsync(
        string sourceContent,
        string extractedText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes similarity for multiple extracted text sections against the source.
    /// </summary>
    /// <param name="sourceContent">The original source content.</param>
    /// <param name="extractedSections">Dictionary of section names to extracted text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A SimilarityReport with the maximum similarity across all sections.</returns>
    Task<SimilarityReport> AnalyzeSectionsAsync(
        string sourceContent,
        Dictionary<string, string> extractedSections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tokenizes text into words for similarity analysis.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>Array of tokens (words).</returns>
    string[] Tokenize(string text);

    /// <summary>
    /// Computes the maximum contiguous token overlap between two token sequences.
    /// </summary>
    /// <param name="sourceTokens">Tokens from the source content.</param>
    /// <param name="extractedTokens">Tokens from the extracted content.</param>
    /// <returns>The length of the longest contiguous matching subsequence.</returns>
    int ComputeMaxContiguousOverlap(string[] sourceTokens, string[] extractedTokens);

    /// <summary>
    /// Computes the n-gram Jaccard similarity between two token sequences.
    /// </summary>
    /// <param name="sourceTokens">Tokens from the source content.</param>
    /// <param name="extractedTokens">Tokens from the extracted content.</param>
    /// <param name="n">The n-gram size (default: 5).</param>
    /// <returns>Jaccard similarity score between 0.0 and 1.0.</returns>
    double ComputeNgramJaccardSimilarity(string[] sourceTokens, string[] extractedTokens, int n = 5);
}
