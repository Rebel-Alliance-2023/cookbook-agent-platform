using System.Text.Json;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Llm;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Implementation of the repair paraphrase service.
/// Uses LLM to rephrase high-similarity content.
/// </summary>
public class RepairParaphraseService : IRepairParaphraseService
{
    private const string RepairPhase = "Ingest.RepairParaphrase";
    private const int MaxSourceExcerptLength = 2000;

    private readonly ILlmRouter _llmRouter;
    private readonly IPromptRepository _promptRepository;
    private readonly ISimilarityDetector _similarityDetector;
    private readonly IngestGuardrailOptions _guardrailOptions;
    private readonly ILogger<RepairParaphraseService> _logger;

    public RepairParaphraseService(
        ILlmRouter llmRouter,
        IPromptRepository promptRepository,
        ISimilarityDetector similarityDetector,
        IOptions<IngestGuardrailOptions> guardrailOptions,
        ILogger<RepairParaphraseService> logger)
    {
        _llmRouter = llmRouter;
        _promptRepository = promptRepository;
        _similarityDetector = similarityDetector;
        _guardrailOptions = guardrailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RepairParaphraseResult> RepairAsync(
        RecipeDraft draft,
        string sourceText,
        SimilarityReport similarityReport,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting repair paraphrase for draft with {Overlap} overlap and {Similarity:P2} similarity",
            similarityReport.MaxContiguousTokenOverlap, similarityReport.MaxNgramSimilarity);

        try
        {
            // Identify sections that need repair
            var sectionsToRepair = IdentifySectionsToRepair(draft, sourceText, similarityReport);

            if (sectionsToRepair.Count == 0)
            {
                _logger.LogInformation("No sections identified for repair");
                return new RepairParaphraseResult
                {
                    Success = true,
                    RepairedDraft = draft,
                    NewSimilarityReport = similarityReport,
                    StillViolatesPolicy = similarityReport.ViolatesPolicy,
                    Details = "No sections required repair."
                };
            }

            // Get the repair prompt template
            var prompt = await GetRepairPromptAsync(sourceText, sectionsToRepair, cancellationToken);
            if (string.IsNullOrEmpty(prompt))
            {
                return new RepairParaphraseResult
                {
                    Success = false,
                    Error = "Could not load repair paraphrase prompt template.",
                    StillViolatesPolicy = true
                };
            }

            // Call LLM for paraphrasing
            var llmResponse = await CallLlmForRephraseAsync(prompt, cancellationToken);
            if (llmResponse == null)
            {
                return new RepairParaphraseResult
                {
                    Success = false,
                    Error = "LLM did not return a valid response.",
                    StillViolatesPolicy = true
                };
            }

            // Parse the rephrased sections
            var rephrasedSections = ParseRephrasedSections(llmResponse);
            if (rephrasedSections.Count == 0)
            {
                return new RepairParaphraseResult
                {
                    Success = false,
                    Error = "Could not parse rephrased sections from LLM response.",
                    RawLlmResponse = llmResponse,
                    StillViolatesPolicy = true
                };
            }

            // Apply repairs to draft
            var repairedDraft = ApplyRepairs(draft, rephrasedSections);

            // Re-run similarity check
            var newSimilarityReport = await RerunSimilarityCheckAsync(
                repairedDraft, sourceText, cancellationToken);

            var stillViolates = newSimilarityReport.ViolatesPolicy;

            // Update the draft with the new similarity report
            repairedDraft = repairedDraft with { SimilarityReport = newSimilarityReport };

            _logger.LogInformation(
                "Repair complete. New similarity: {Overlap} overlap, {Similarity:P2}. Still violates: {StillViolates}",
                newSimilarityReport.MaxContiguousTokenOverlap,
                newSimilarityReport.MaxNgramSimilarity,
                stillViolates);

            return new RepairParaphraseResult
            {
                Success = !stillViolates,
                RepairedDraft = repairedDraft,
                NewSimilarityReport = newSimilarityReport,
                StillViolatesPolicy = stillViolates,
                RawLlmResponse = llmResponse,
                Details = $"Repaired {rephrasedSections.Count} section(s). " +
                         $"New similarity: {newSimilarityReport.MaxNgramSimilarity:P2}, " +
                         $"overlap: {newSimilarityReport.MaxContiguousTokenOverlap} tokens."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repair paraphrase failed");
            return new RepairParaphraseResult
            {
                Success = false,
                Error = ex.Message,
                StillViolatesPolicy = true
            };
        }
    }

    /// <summary>
    /// Identifies sections that need to be repaired based on similarity.
    /// </summary>
    private List<SectionToRephrase> IdentifySectionsToRepair(
        RecipeDraft draft,
        string sourceText,
        SimilarityReport report)
    {
        var sections = new List<SectionToRephrase>();
        var recipe = draft.Recipe;

        // Check Description
        if (!string.IsNullOrWhiteSpace(recipe.Description))
        {
            var descTokens = _similarityDetector.Tokenize(recipe.Description);
            var sourceTokens = _similarityDetector.Tokenize(sourceText);
            var overlap = _similarityDetector.ComputeMaxContiguousOverlap(sourceTokens, descTokens);
            var similarity = _similarityDetector.ComputeNgramJaccardSimilarity(sourceTokens, descTokens);

            if (overlap >= _guardrailOptions.TokenOverlapWarningThreshold ||
                similarity >= _guardrailOptions.NgramSimilarityWarningThreshold)
            {
                sections.Add(new SectionToRephrase
                {
                    Name = "Description",
                    OriginalText = recipe.Description,
                    TokenOverlap = overlap,
                    SimilarityScore = similarity
                });
            }
        }

        // Check Instructions (combined)
        if (recipe.Instructions.Count > 0)
        {
            var instructionsText = string.Join(" ", recipe.Instructions);
            var instrTokens = _similarityDetector.Tokenize(instructionsText);
            var sourceTokens = _similarityDetector.Tokenize(sourceText);
            var overlap = _similarityDetector.ComputeMaxContiguousOverlap(sourceTokens, instrTokens);
            var similarity = _similarityDetector.ComputeNgramJaccardSimilarity(sourceTokens, instrTokens);

            if (overlap >= _guardrailOptions.TokenOverlapWarningThreshold ||
                similarity >= _guardrailOptions.NgramSimilarityWarningThreshold)
            {
                sections.Add(new SectionToRephrase
                {
                    Name = "Instructions",
                    OriginalText = instructionsText,
                    TokenOverlap = overlap,
                    SimilarityScore = similarity
                });
            }
        }

        return sections;
    }

    /// <summary>
    /// Builds the repair prompt using the template.
    /// </summary>
    private async Task<string?> GetRepairPromptAsync(
        string sourceText,
        List<SectionToRephrase> sections,
        CancellationToken cancellationToken)
    {
        // Try to get the active prompt template
        var template = await _promptRepository.GetActiveByPhaseAsync(RepairPhase, cancellationToken);
        
        if (template == null)
        {
            _logger.LogWarning("No active prompt template found for phase {Phase}, using fallback", RepairPhase);
            return BuildFallbackPrompt(sourceText, sections);
        }

        // For now, use the fallback prompt builder
        // TODO: Integrate with ScribanPromptRenderer when available
        return BuildFallbackPrompt(sourceText, sections);
    }

    /// <summary>
    /// Builds a fallback prompt if template is not available.
    /// </summary>
    private string BuildFallbackPrompt(string sourceText, List<SectionToRephrase> sections)
    {
        var sourceExcerpt = sourceText.Length > MaxSourceExcerptLength
            ? sourceText[..MaxSourceExcerptLength] + "..."
            : sourceText;

        var sectionsJson = JsonSerializer.Serialize(sections.Select(s => new
        {
            name = s.Name,
            original_text = s.OriginalText,
            similarity_score = s.SimilarityScore,
            token_overlap = s.TokenOverlap
        }), new JsonSerializerOptions { WriteIndented = true });

        return $@"You are a recipe content editor. Rephrase the following recipe sections to reduce verbatim similarity with the source while preserving all factual information.

## Source Text Excerpt
{sourceExcerpt}

## Sections to Rephrase
{sectionsJson}

## Instructions
1. Preserve all factual information - ingredients, quantities, temperatures, times
2. Rephrase in your own words - change sentence structure and word choices
3. Maintain clarity and natural language

## Output Format
Return ONLY a JSON object with the rephrased sections:
{{
  ""sections"": [
    {{
      ""name"": ""Description"",
      ""rephrased_text"": ""Your rephrased description...""
    }}
  ]
}}";
    }

    /// <summary>
    /// Calls the LLM to get rephrased content.
    /// </summary>
    private async Task<string?> CallLlmForRephraseAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _llmRouter.ChatAsync(new LlmRequest
            {
                Messages = [new LlmMessage { Role = "user", Content = prompt }],
                Provider = "OpenAI", // Explicitly use OpenAI for repair to avoid Anthropic model issues
                MaxTokens = 2000,
                Temperature = 0.7 // Slightly higher for creative rephrasing
            }, cancellationToken);

            return response?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed for repair paraphrase");
            return null;
        }
    }

    /// <summary>
    /// Parses the rephrased sections from LLM response.
    /// </summary>
    private List<RephrasedSection> ParseRephrasedSections(string llmResponse)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');
            
            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("Could not find JSON in LLM response");
                return [];
            }

            var json = llmResponse[jsonStart..(jsonEnd + 1)];
            var result = JsonSerializer.Deserialize<RephraseResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Sections?.Select(s => new RephrasedSection
            {
                Name = s.Name ?? "",
                RephrasedText = s.RephrasedText ?? s.Rephrased_Text ?? ""
            }).Where(s => !string.IsNullOrWhiteSpace(s.RephrasedText)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse rephrased sections from LLM response");
            return [];
        }
    }

    /// <summary>
    /// Applies the rephrased sections to the draft.
    /// </summary>
    private RecipeDraft ApplyRepairs(RecipeDraft draft, List<RephrasedSection> rephrasedSections)
    {
        var newRecipe = draft.Recipe;
        
        foreach (var section in rephrasedSections)
        {
            switch (section.Name.ToLowerInvariant())
            {
                case "description":
                    newRecipe = newRecipe with { Description = section.RephrasedText };
                    break;
                    
                case "instructions":
                    // Split rephrased instructions back into steps
                    var steps = SplitIntoSteps(section.RephrasedText);
                    newRecipe = newRecipe with { Instructions = steps };
                    break;
            }
        }

        return draft with { Recipe = newRecipe };
    }

    /// <summary>
    /// Splits rephrased instructions text back into individual steps.
    /// </summary>
    private List<string> SplitIntoSteps(string instructionsText)
    {
        // Try to split by numbered patterns or periods
        var lines = instructionsText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\d+[\.\)]\s*", ""))
            .ToList();

        if (lines.Count > 1)
            return lines;

        // If no line breaks, try to split by sentences
        return instructionsText
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim() + ".")
            .Where(s => s.Length > 5)
            .ToList();
    }

    /// <summary>
    /// Re-runs similarity check on the repaired draft.
    /// </summary>
    private async Task<SimilarityReport> RerunSimilarityCheckAsync(
        RecipeDraft draft,
        string sourceText,
        CancellationToken cancellationToken)
    {
        var sections = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(draft.Recipe.Description))
        {
            sections["Description"] = draft.Recipe.Description;
        }

        if (draft.Recipe.Instructions.Count > 0)
        {
            sections["Instructions"] = string.Join(" ", draft.Recipe.Instructions);
        }

        return await _similarityDetector.AnalyzeSectionsAsync(sourceText, sections, cancellationToken);
    }

    // Helper classes for JSON parsing
    private class RephraseResponse
    {
        public List<RephraseSection>? Sections { get; set; }
    }

    private class RephraseSection
    {
        public string? Name { get; set; }
        public string? RephrasedText { get; set; }
        public string? Rephrased_Text { get; set; }
    }
}
