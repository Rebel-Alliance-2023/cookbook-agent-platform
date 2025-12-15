using System.Text.Json;
using Cookbook.Platform.Client.Blazor.Services;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Shared.Models.Ingest.Search;
using Microsoft.AspNetCore.Components;

namespace Cookbook.Platform.Client.Blazor.Components.Pages;

public partial class ReviewReady
{
    [Parameter] public string? TaskId { get; set; }

    [Inject] private ApiClientService ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<ReviewReady> Logger { get; set; } = default!;

    private RecipeDraft? draft;
    private bool isLoading = true;
    private string? errorMessage;
    private bool isCommitting;
    private bool isNormalizing;
    private bool showRejectDialog;
    private string? rejectReason;
    private string? successMessage;
    private string? committedRecipeId;
    private string? taskStatus;
    private bool isTerminalState;
    
    // Search discovery candidates
    private List<SearchCandidate>? searchCandidates;
    private int selectedCandidateIndex = 0;
    
    // Repair tracking
    private bool repairAttempted;
    private bool repairSuccessful;
    
    // Similarity thresholds (should match IngestGuardrailOptions)
    private const int WarningOverlapThreshold = 25;
    private const int ErrorOverlapThreshold = 50;
    private const double WarningSimilarityThreshold = 0.5;
    private const double ErrorSimilarityThreshold = 0.7;

    protected override async Task OnInitializedAsync()
    {
        await LoadDraft();
        await LoadTaskState();
        await LoadSearchCandidates();
    }

    private async Task LoadDraft()
    {
        if (string.IsNullOrEmpty(TaskId))
        {
            errorMessage = "No task ID provided.";
            isLoading = false;
            return;
        }

        try
        {
            draft = await ApiClient.GetRecipeDraftAsync(TaskId);
            
            if (draft == null)
            {
                errorMessage = "Recipe draft not found. The import may still be in progress.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load recipe draft for task {TaskId}", TaskId);
            errorMessage = $"Failed to load recipe: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadTaskState()
    {
        if (string.IsNullOrEmpty(TaskId))
            return;

        try
        {
            var state = await ApiClient.GetTaskStateAsync(TaskId);
            if (state != null)
            {
                taskStatus = state.Status;
                isTerminalState = IsTerminalStatus(state.Status);
                
                // If already committed, show success state
                if (state.Status == "Committed")
                {
                    successMessage = "Recipe has been added to your cookbook!";
                    // Try to get the recipe ID from the result
                    committedRecipeId = state.CurrentPhase == "Committed" ? TaskId : null;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load task state for {TaskId}", TaskId);
        }
    }

    private async Task LoadSearchCandidates()
    {
        if (string.IsNullOrEmpty(TaskId))
            return;

        try
        {
            // Try to load candidates.normalized.json artifact
            var candidatesData = await ApiClient.DownloadArtifactAsync(TaskId, "candidates.normalized.json");
            if (candidatesData != null && candidatesData.Length > 0)
            {
                var json = System.Text.Encoding.UTF8.GetString(candidatesData);
                searchCandidates = JsonSerializer.Deserialize<List<SearchCandidate>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                Logger.LogInformation("Loaded {Count} search candidates for task {TaskId}", 
                    searchCandidates?.Count ?? 0, TaskId);
            }
        }
        catch (Exception ex)
        {
            // It's OK if candidates are not available (URL mode doesn't have them)
            Logger.LogDebug(ex, "Could not load search candidates for task {TaskId}", TaskId);
        }
    }

    private static bool IsTerminalStatus(string? status) => status switch
    {
        "Committed" => true,
        "Rejected" => true,
        "Expired" => true,
        "Failed" => true,
        "Cancelled" => true,
        _ => false
    };

    private async Task CommitRecipe()
    {
        if (draft == null || !draft.ValidationReport.IsValid || isTerminalState)
            return;

        isCommitting = true;
        errorMessage = null;

        try
        {
            var result = await ApiClient.CommitRecipeDraftAsync(TaskId!);
            
            if (result)
            {
                Logger.LogInformation("Recipe committed for task {TaskId}", TaskId);
                successMessage = "Recipe has been added to your cookbook!";
                committedRecipeId = draft.Recipe.Id;
                taskStatus = "Committed";
                isTerminalState = true;
            }
            else
            {
                errorMessage = "Failed to save recipe. Please try again.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to commit recipe for task {TaskId}", TaskId);
            errorMessage = $"Failed to save recipe: {ex.Message}";
        }
        finally
        {
            isCommitting = false;
        }
    }

    private async Task RejectRecipe()
    {
        if (isTerminalState)
            return;

        try
        {
            var success = await ApiClient.RejectRecipeDraftAsync(TaskId!, rejectReason);
            
            if (success)
            {
                Logger.LogInformation("Recipe rejected for task {TaskId}", TaskId);
                taskStatus = "Rejected";
                isTerminalState = true;
                showRejectDialog = false;
                Navigation.NavigateTo("/ingest");
            }
            else
            {
                errorMessage = "Failed to reject recipe. Please try again.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reject recipe for task {TaskId}", TaskId);
            errorMessage = $"Failed to reject recipe: {ex.Message}";
        }
        finally
        {
            showRejectDialog = false;
        }
    }

    private void NavigateToIngest()
    {
        Navigation.NavigateTo("/ingest");
    }

    private void TryDifferentCandidate()
    {
        // Navigate back to ingest page in Query mode
        Navigation.NavigateTo("/ingest");
    }

    private void ShowRejectDialog()
    {
        if (!isTerminalState)
        {
            showRejectDialog = true;
        }
    }

    private void HideRejectDialog()
    {
        showRejectDialog = false;
    }

    private async Task StartNormalize()
    {
        if (draft == null || isTerminalState)
            return;

        isNormalizing = true;
        errorMessage = null;

        try
        {
            // Create a normalize task for this recipe
            var recipeId = draft.Recipe.Id;
            if (string.IsNullOrEmpty(recipeId))
            {
                errorMessage = "Cannot normalize: recipe has no ID. Please commit the recipe first.";
                return;
            }

            var normalizeTaskId = await ApiClient.CreateNormalizeTaskAsync(recipeId);
            
            if (!string.IsNullOrEmpty(normalizeTaskId))
            {
                Logger.LogInformation("Created normalize task {NormalizeTaskId} for recipe {RecipeId}", 
                    normalizeTaskId, recipeId);
                // Navigate to normalize review page
                Navigation.NavigateTo($"/ingest/normalize/{normalizeTaskId}");
            }
            else
            {
                errorMessage = "Failed to start normalization. Please try again.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start normalize for task {TaskId}", TaskId);
            errorMessage = $"Failed to start normalization: {ex.Message}";
        }
        finally
        {
            isNormalizing = false;
        }
    }

    private string GetTerminalStateClass() => taskStatus switch
    {
        "Committed" => "state-committed",
        "Rejected" => "state-rejected",
        "Expired" => "state-expired",
        "Failed" => "state-failed",
        _ => "state-unknown"
    };

    private MarkupString GetTerminalStateIcon() => taskStatus switch
    {
        "Committed" => new MarkupString(@"<svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><path d=""M22 11.08V12a10 10 0 1 1-5.93-9.14""/><polyline points=""22 4 12 14.01 9 11.01""/></svg>"),
        "Rejected" => new MarkupString(@"<svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><circle cx=""12"" cy=""12"" r=""10""/><line x1=""15"" y1=""9"" x2=""9"" y2=""15""/><line x1=""9"" y1=""9"" x2=""15"" y2=""15""/></svg>"),
        "Expired" => new MarkupString(@"<svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><circle cx=""12"" cy=""12"" r=""10""/><polyline points=""12 6 12 12 16 14""/></svg>"),
        "Failed" => new MarkupString(@"<svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><circle cx=""12"" cy=""12"" r=""10""/><line x1=""12"" y1=""8"" x2=""12"" y2=""12""/><line x1=""12"" y1=""16"" x2=""12.01"" y2=""16""/></svg>"),
        _ => new MarkupString(@"<svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><circle cx=""12"" cy=""12"" r=""10""/></svg>")
    };

    private string GetTerminalStateMessage() => taskStatus switch
    {
        "Committed" => "This recipe has been added to your cookbook",
        "Rejected" => "This recipe was rejected",
        "Expired" => "This draft has expired",
        "Failed" => "This import failed",
        _ => "This task is in a terminal state"
    };

    private static string FormatTime(int minutes)
    {
        if (minutes == 0)
            return "N/A";
        if (minutes < 60)
            return $"{minutes} min";
        
        var hours = minutes / 60;
        var mins = minutes % 60;
        return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
    }

    private static string FormatQuantity(double quantity)
    {
        if (quantity == Math.Floor(quantity))
            return ((int)quantity).ToString();
        
        var fraction = quantity - Math.Floor(quantity);
        var whole = (int)Math.Floor(quantity);
        
        var fractionStr = fraction switch
        {
            0.25 => "¼",
            0.33 or 0.333 => "?",
            0.5 => "½",
            0.66 or 0.667 => "?",
            0.75 => "¾",
            _ => fraction.ToString("0.##")
        };

        return whole > 0 ? $"{whole} {fractionStr}" : fractionStr;
    }
    
    // Similarity helper methods
    private bool HasHighSimilarity()
    {
        if (draft?.SimilarityReport == null) return false;
        return draft.SimilarityReport.MaxContiguousTokenOverlap >= WarningOverlapThreshold ||
               draft.SimilarityReport.MaxNgramSimilarity >= WarningSimilarityThreshold;
    }
    
    private bool SimilarityBlocksCommit()
    {
        if (draft?.SimilarityReport == null) return false;
        return draft.SimilarityReport.ViolatesPolicy && !repairSuccessful;
    }
    
    private string GetOverlapClass()
    {
        if (draft?.SimilarityReport == null) return "value-ok";
        var overlap = draft.SimilarityReport.MaxContiguousTokenOverlap;
        if (overlap >= ErrorOverlapThreshold) return "value-error";
        if (overlap >= WarningOverlapThreshold) return "value-warning";
        return "value-ok";
    }
    
    private string GetSimilarityClass()
    {
        if (draft?.SimilarityReport == null) return "value-ok";
        var similarity = draft.SimilarityReport.MaxNgramSimilarity;
        if (similarity >= ErrorSimilarityThreshold) return "value-error";
        if (similarity >= WarningSimilarityThreshold) return "value-warning";
        return "value-ok";
    }
}
