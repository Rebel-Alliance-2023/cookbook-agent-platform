using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Service for normalizing recipe data using LLM-generated JSON patches.
/// </summary>
public interface INormalizeService
{
    /// <summary>
    /// Generates normalization patches for a recipe.
    /// </summary>
    /// <param name="recipe">The recipe to normalize.</param>
    /// <param name="focusAreas">Optional focus areas to prioritize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The normalize patch response from the LLM.</returns>
    Task<NormalizePatchResponse> GeneratePatchesAsync(
        Recipe recipe,
        IReadOnlyList<string>? focusAreas = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies normalization patches to a recipe (test apply - returns modified copy).
    /// </summary>
    /// <param name="recipe">The original recipe.</param>
    /// <param name="patches">The patches to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of applying the patches.</returns>
    Task<NormalizePatchResult> ApplyPatchesAsync(
        Recipe recipe,
        IReadOnlyList<NormalizePatchOperation> patches,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a set of patches against a recipe.
    /// </summary>
    /// <param name="recipe">The recipe to validate patches against.</param>
    /// <param name="patches">The patches to validate.</param>
    /// <returns>List of validation errors, empty if valid.</returns>
    IReadOnlyList<string> ValidatePatches(Recipe recipe, IReadOnlyList<NormalizePatchOperation> patches);
}
