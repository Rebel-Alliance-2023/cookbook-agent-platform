using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Storage;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Background service that monitors and expires stale recipe drafts in ReviewReady state.
/// Runs periodically to transition expired drafts to Expired status.
/// </summary>
public class DraftExpirationService : BackgroundService
{
    private readonly CosmosClient _cosmosClient;
    private readonly IMessagingBus _messagingBus;
    private readonly IngestOptions _options;
    private readonly CosmosOptions _cosmosOptions;
    private readonly ILogger<DraftExpirationService> _logger;

    /// <summary>
    /// Interval between expiration checks. Default: 1 hour.
    /// </summary>
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public DraftExpirationService(
        CosmosClient cosmosClient,
        IMessagingBus messagingBus,
        IOptions<IngestOptions> options,
        IOptions<CosmosOptions> cosmosOptions,
        ILogger<DraftExpirationService> logger)
    {
        _cosmosClient = cosmosClient;
        _messagingBus = messagingBus;
        _options = options.Value;
        _cosmosOptions = cosmosOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Draft expiration service starting. Check interval: {Interval}, Expiration window: {Window} days",
            _checkInterval,
            _options.DraftExpirationDays);

        // Wait a short time before first run to allow system to initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExpireStaleTasksAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during expiration check");
            }

            // Wait for next check interval
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("Draft expiration service stopped");
    }

    /// <summary>
    /// Checks for stale ReviewReady tasks and transitions them to Expired status.
    /// </summary>
    private async Task CheckAndExpireStaleTasksAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting expiration check at {Time}", startTime);

        var container = _cosmosClient.GetContainer(_cosmosOptions.DatabaseName, "tasks");
        var expirationThreshold = DateTime.UtcNow.Add(-_options.DraftExpiration);

        // Query for Ingest tasks that haven't been updated recently
        // We'll check their actual state in Redis to confirm they're in ReviewReady
        var query = new QueryDefinition(
            @"SELECT * FROM c 
              WHERE c.AgentType = 'Ingest' 
              AND c.CreatedAt < @threshold")
            .WithParameter("@threshold", expirationThreshold);

        var candidateTasks = new List<AgentTask>();
        using var iterator = container.GetItemQueryIterator<AgentTask>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            candidateTasks.AddRange(response);
        }

        _logger.LogInformation(
            "Found {Count} candidate tasks created before {Threshold}",
            candidateTasks.Count,
            expirationThreshold);

        var expiredCount = 0;
        var skippedCount = 0;

        foreach (var task in candidateTasks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Get current state from Redis
                var taskState = await _messagingBus.GetTaskStateAsync(task.TaskId, cancellationToken);

                if (taskState == null)
                {
                    _logger.LogWarning("Task {TaskId} has no state in Redis, skipping", task.TaskId);
                    skippedCount++;
                    continue;
                }

                // Only expire tasks in ReviewReady status
                if (taskState.Status != AgentTaskStatus.ReviewReady)
                {
                    skippedCount++;
                    continue;
                }

                // Check if the task has actually expired
                var reviewReadyTime = taskState.LastUpdated;
                var expirationTime = reviewReadyTime.Add(_options.DraftExpiration);

                if (DateTime.UtcNow <= expirationTime)
                {
                    // Not expired yet
                    skippedCount++;
                    continue;
                }

                // Transition to Expired status
                await TransitionToExpiredAsync(task.TaskId, taskState, reviewReadyTime, cancellationToken);
                expiredCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking task {TaskId} for expiration", task.TaskId);
            }
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Expiration check completed in {Duration}ms. Expired: {Expired}, Skipped: {Skipped}",
            duration.TotalMilliseconds,
            expiredCount,
            skippedCount);
    }

    /// <summary>
    /// Transitions a task to Expired status in Redis.
    /// </summary>
    private async Task TransitionToExpiredAsync(
        string taskId,
        TaskState currentState,
        DateTime reviewReadyTime,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredState = new TaskState
        {
            TaskId = taskId,
            Status = AgentTaskStatus.Expired,
            Progress = currentState.Progress,
            CurrentPhase = "Expired",
            Result = $"Draft expired after {_options.DraftExpirationDays} days (ReviewReady at {reviewReadyTime:O})",
            LastUpdated = now
        };

        await _messagingBus.SetTaskStateAsync(taskId, expiredState, ttl: null, cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} transitioned to Expired. Was ReviewReady at {ReviewReadyTime}, expired at {ExpiredTime}",
            taskId,
            reviewReadyTime,
            now);
    }
}
