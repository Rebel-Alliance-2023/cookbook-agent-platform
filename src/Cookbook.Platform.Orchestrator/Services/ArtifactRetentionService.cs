using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Storage;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Background service that cleans up expired artifacts based on retention policies.
/// - Committed tasks: 180 days retention (default)
/// - Non-committed tasks (Rejected, Expired, Failed, Cancelled): 30 days retention (default)
/// </summary>
public class ArtifactRetentionService : BackgroundService
{
    private readonly IBlobStorage _blobStorage;
    private readonly CosmosClient _cosmosClient;
    private readonly IMessagingBus _messagingBus;
    private readonly ArtifactRetentionOptions _options;
    private readonly CosmosOptions _cosmosOptions;
    private readonly ILogger<ArtifactRetentionService> _logger;

    /// <summary>
    /// Metadata key for tracking when an artifact was created.
    /// </summary>
    public const string CreatedAtMetadataKey = "CreatedAt";

    /// <summary>
    /// Metadata key for tracking the task status at time of last update.
    /// </summary>
    public const string TaskStatusMetadataKey = "TaskStatus";

    /// <summary>
    /// Metadata key for tracking the thread ID.
    /// </summary>
    public const string ThreadIdMetadataKey = "ThreadId";

    /// <summary>
    /// Metadata key for tracking the task ID.
    /// </summary>
    public const string TaskIdMetadataKey = "TaskId";

    public ArtifactRetentionService(
        IBlobStorage blobStorage,
        CosmosClient cosmosClient,
        IMessagingBus messagingBus,
        IOptions<ArtifactRetentionOptions> options,
        IOptions<CosmosOptions> cosmosOptions,
        ILogger<ArtifactRetentionService> logger)
    {
        _blobStorage = blobStorage;
        _cosmosClient = cosmosClient;
        _messagingBus = messagingBus;
        _options = options.Value;
        _cosmosOptions = cosmosOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCleanup)
        {
            _logger.LogInformation("Artifact retention cleanup is disabled");
            return;
        }

        _logger.LogInformation(
            "Artifact retention service starting. Cleanup interval: {Interval}, " +
            "Committed retention: {CommittedDays} days, Non-committed retention: {NonCommittedDays} days",
            _options.CleanupInterval,
            _options.CommittedRetentionDays,
            _options.NonCommittedRetentionDays);

        // Wait before first run to allow system to initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredArtifactsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during artifact retention cleanup");
            }

            // Wait for next cleanup interval
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Artifact retention service stopped");
    }

    /// <summary>
    /// Cleans up artifacts that have exceeded their retention period.
    /// </summary>
    private async Task CleanupExpiredArtifactsAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting artifact retention cleanup at {Time}", startTime);

        var committedThreshold = DateTime.UtcNow.AddDays(-_options.CommittedRetentionDays);
        var nonCommittedThreshold = DateTime.UtcNow.AddDays(-_options.NonCommittedRetentionDays);

        var deletedCount = 0;
        var processedCount = 0;

        // Get all unique task prefixes (threadId/taskId combinations)
        var blobs = await _blobStorage.ListAsync(cancellationToken: cancellationToken);
        var taskPrefixes = blobs
            .Select(b => GetTaskPrefix(b))
            .Where(p => p != null)
            .Distinct()
            .ToList();

        _logger.LogInformation("Found {Count} unique task artifact groups to evaluate", taskPrefixes.Count);

        foreach (var prefix in taskPrefixes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (deletedCount >= _options.MaxDeletesPerRun)
            {
                _logger.LogInformation("Reached max deletes per run ({Max}), stopping cleanup", _options.MaxDeletesPerRun);
                break;
            }

            try
            {
                var (threadId, taskId) = ParseTaskPrefix(prefix!);
                if (threadId == null || taskId == null)
                    continue;

                processedCount++;

                // Get task state to determine retention period
                var taskState = await _messagingBus.GetTaskStateAsync(taskId, cancellationToken);
                var isCommitted = taskState?.Status == AgentTaskStatus.Committed;
                var threshold = isCommitted ? committedThreshold : nonCommittedThreshold;

                // Get task creation date from Cosmos (or metadata if available)
                var taskCreatedAt = await GetTaskCreationDateAsync(taskId, cancellationToken);
                if (taskCreatedAt == null)
                {
                    // Task not found in Cosmos, assume it's old and use non-committed threshold
                    taskCreatedAt = DateTime.UtcNow.AddDays(-_options.NonCommittedRetentionDays - 1);
                }

                if (taskCreatedAt < threshold)
                {
                    // Delete all artifacts for this task
                    var taskBlobs = blobs.Where(b => b.StartsWith(prefix!)).ToList();
                    foreach (var blob in taskBlobs)
                    {
                        await _blobStorage.DeleteAsync(blob, cancellationToken);
                        deletedCount++;
                    }

                    _logger.LogDebug(
                        "Deleted {Count} artifacts for task {TaskId} (committed: {IsCommitted}, created: {Created})",
                        taskBlobs.Count, taskId, isCommitted, taskCreatedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing artifacts for prefix {Prefix}", prefix);
            }
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Artifact retention cleanup completed. Processed: {Processed}, Deleted: {Deleted}, Duration: {Duration}",
            processedCount, deletedCount, duration);
    }

    /// <summary>
    /// Gets the task creation date from Cosmos DB.
    /// </summary>
    private async Task<DateTime?> GetTaskCreationDateAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_cosmosOptions.DatabaseName, "tasks");
            
            // Query for the task
            var query = new QueryDefinition("SELECT c.CreatedAt FROM c WHERE c.TaskId = @taskId")
                .WithParameter("@taskId", taskId);

            using var iterator = container.GetItemQueryIterator<dynamic>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var task = response.FirstOrDefault();
                if (task != null)
                {
                    return (DateTime)task.CreatedAt;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve task creation date for {TaskId}", taskId);
        }

        return null;
    }

    /// <summary>
    /// Extracts the task prefix (threadId/taskId) from a blob path.
    /// </summary>
    private static string? GetTaskPrefix(string blobPath)
    {
        var parts = blobPath.Split('/');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}/{parts[1]}";
        }
        return null;
    }

    /// <summary>
    /// Parses thread ID and task ID from a task prefix.
    /// </summary>
    private static (string? threadId, string? taskId) ParseTaskPrefix(string prefix)
    {
        var parts = prefix.Split('/');
        if (parts.Length >= 2)
        {
            return (parts[0], parts[1]);
        }
        return (null, null);
    }
}
