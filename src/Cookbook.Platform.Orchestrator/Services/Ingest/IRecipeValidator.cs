using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Orchestrator.Services.Ingest;

/// <summary>
/// Service for validating extracted recipes before review.
/// </summary>
public interface IRecipeValidator
{
    /// <summary>
    /// Validates a recipe and returns a validation report.
    /// </summary>
    /// <param name="recipe">The recipe to validate.</param>
    /// <returns>A validation report with errors and warnings.</returns>
    ValidationReport Validate(Recipe recipe);

    /// <summary>
    /// Validates a recipe asynchronously with optional external checks.
    /// </summary>
    /// <param name="recipe">The recipe to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation report with errors and warnings.</returns>
    Task<ValidationReport> ValidateAsync(Recipe recipe, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation rule severity level.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Error that blocks commit.
    /// </summary>
    Error,
    
    /// <summary>
    /// Warning that should be reviewed but doesn't block commit.
    /// </summary>
    Warning
}

/// <summary>
/// Represents a single validation issue.
/// </summary>
public record ValidationIssue
{
    /// <summary>
    /// The field or property that has the issue.
    /// </summary>
    public required string Field { get; init; }
    
    /// <summary>
    /// Description of the issue.
    /// </summary>
    public required string Message { get; init; }
    
    /// <summary>
    /// The severity of the issue.
    /// </summary>
    public ValidationSeverity Severity { get; init; }
    
    /// <summary>
    /// A code for the validation rule.
    /// </summary>
    public string? Code { get; init; }
}
