using Cookbook.Platform.Shared.Models.Prompts;

namespace Cookbook.Platform.Storage.Repositories;

/// <summary>
/// Repository interface for prompt template CRUD operations.
/// </summary>
public interface IPromptRepository
{
    /// <summary>
    /// Gets a prompt template by ID and phase.
    /// </summary>
    /// <param name="id">The prompt template ID.</param>
    /// <param name="phase">The phase (partition key) for efficient lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prompt template, or null if not found.</returns>
    Task<PromptTemplate?> GetByIdAsync(string id, string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active prompt template for a specific phase.
    /// </summary>
    /// <param name="phase">The phase to get the active template for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active prompt template, or null if none is active.</returns>
    Task<PromptTemplate?> GetActiveByPhaseAsync(string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all prompt templates for a specific phase.
    /// </summary>
    /// <param name="phase">The phase to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of prompt templates for the phase.</returns>
    Task<List<PromptTemplate>> GetByPhaseAsync(string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all prompt templates across all phases.
    /// Note: This is a cross-partition query and should be used sparingly.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all prompt templates.</returns>
    Task<List<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new prompt template.
    /// </summary>
    /// <param name="template">The prompt template to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created prompt template.</returns>
    Task<PromptTemplate> CreateAsync(PromptTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing prompt template.
    /// </summary>
    /// <param name="template">The prompt template with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated prompt template.</returns>
    Task<PromptTemplate> UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a prompt template, deactivating any other active template for the same phase.
    /// </summary>
    /// <param name="id">The ID of the template to activate.</param>
    /// <param name="phase">The phase of the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated prompt template.</returns>
    Task<PromptTemplate> ActivateAsync(string id, string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a prompt template.
    /// </summary>
    /// <param name="id">The ID of the template to deactivate.</param>
    /// <param name="phase">The phase of the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated prompt template.</returns>
    Task<PromptTemplate> DeactivateAsync(string id, string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a prompt template.
    /// </summary>
    /// <param name="id">The ID of the template to delete.</param>
    /// <param name="phase">The phase of the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string id, string phase, CancellationToken cancellationToken = default);
}
