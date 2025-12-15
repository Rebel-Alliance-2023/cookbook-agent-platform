namespace Cookbook.Platform.Client.Blazor.Components.Shared;

/// <summary>
/// Event arguments for the Apply Patch action in NormalizeDiff component.
/// </summary>
public class ApplyPatchEventArgs
{
    /// <summary>
    /// The task ID that generated the patches.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The recipe ID to apply patches to.
    /// </summary>
    public required string RecipeId { get; init; }

    /// <summary>
    /// Indices of selected patches to apply.
    /// </summary>
    public List<int> SelectedIndices { get; init; } = [];
}
