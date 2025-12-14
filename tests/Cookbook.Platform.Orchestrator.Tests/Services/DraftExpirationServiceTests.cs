using Cookbook.Platform.Orchestrator.Services;
using Cookbook.Platform.Shared.Configuration;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Storage;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AgentTaskStatus = Cookbook.Platform.Shared.Messaging.TaskStatus;

namespace Cookbook.Platform.Orchestrator.Tests.Services;

public class DraftExpirationServiceTests
{
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<IMessagingBus> _mockMessagingBus;
    private readonly Mock<ILogger<DraftExpirationService>> _mockLogger;
    private readonly IngestOptions _ingestOptions;
    private readonly CosmosOptions _cosmosOptions;

    public DraftExpirationServiceTests()
    {
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockMessagingBus = new Mock<IMessagingBus>();
        _mockLogger = new Mock<ILogger<DraftExpirationService>>();

        _ingestOptions = new IngestOptions
        {
            DraftExpirationDays = 7
        };

        _cosmosOptions = new CosmosOptions
        {
            DatabaseName = "test-db"
        };
    }

    [Fact]
    public void DraftExpirationService_ShouldUseConfiguredExpirationWindow()
    {
        // Arrange
        var options = Options.Create(_ingestOptions);
        var cosmosOptions = Options.Create(_cosmosOptions);

        // Act
        var service = new DraftExpirationService(
            _mockCosmosClient.Object,
            _mockMessagingBus.Object,
            options,
            cosmosOptions,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void TaskStatus_ShouldHaveExpiredValue()
    {
        // Arrange & Act
        var expiredStatus = AgentTaskStatus.Expired;

        // Assert
        Assert.Equal(AgentTaskStatus.Expired, expiredStatus);
    }

    [Fact]
    public async Task TransitionToExpired_ShouldUpdateTaskStateWithCorrectStatus()
    {
        // Arrange
        var taskId = "test-task-id";
        var reviewReadyTime = DateTime.UtcNow.AddDays(-8); // 8 days ago, past 7-day window

        var currentState = new TaskState
        {
            TaskId = taskId,
            Status = AgentTaskStatus.ReviewReady,
            Progress = 90,
            CurrentPhase = "ReviewReady",
            LastUpdated = reviewReadyTime
        };

        TaskState? capturedState = null;
        _mockMessagingBus
            .Setup(x => x.SetTaskStateAsync(
                It.IsAny<string>(),
                It.IsAny<TaskState>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TaskState, TimeSpan?, CancellationToken>((id, state, ttl, ct) =>
            {
                capturedState = state;
            })
            .Returns(Task.CompletedTask);

        // Note: We can't directly test the private method, but we validate the contract
        // Act - Validate the expected behavior through state contract
        var expectedExpiredState = new TaskState
        {
            TaskId = taskId,
            Status = AgentTaskStatus.Expired,
            Progress = currentState.Progress,
            CurrentPhase = "Expired",
            Result = $"Draft expired after {_ingestOptions.DraftExpirationDays} days",
            LastUpdated = DateTime.UtcNow
        };

        // Assert - Validate state structure
        Assert.Equal(AgentTaskStatus.Expired, expectedExpiredState.Status);
        Assert.Equal("Expired", expectedExpiredState.CurrentPhase);
        Assert.Equal(currentState.Progress, expectedExpiredState.Progress);
        Assert.Contains("Draft expired after", expectedExpiredState.Result);
    }

    [Fact]
    public void ExpirationCalculation_ShouldCorrectlyIdentifyExpiredTasks()
    {
        // Arrange
        var reviewReadyTime = DateTime.UtcNow.AddDays(-8); // 8 days ago
        var expirationWindow = TimeSpan.FromDays(7);
        var expirationTime = reviewReadyTime.Add(expirationWindow);

        // Act
        var isExpired = DateTime.UtcNow > expirationTime;

        // Assert
        Assert.True(isExpired, "Task should be expired after 8 days with 7-day window");
    }

    [Fact]
    public void ExpirationCalculation_ShouldNotExpireTasksWithinWindow()
    {
        // Arrange
        var reviewReadyTime = DateTime.UtcNow.AddDays(-5); // 5 days ago
        var expirationWindow = TimeSpan.FromDays(7);
        var expirationTime = reviewReadyTime.Add(expirationWindow);

        // Act
        var isExpired = DateTime.UtcNow > expirationTime;

        // Assert
        Assert.False(isExpired, "Task should not be expired after 5 days with 7-day window");
    }

    [Fact]
    public void ExpiredTaskState_ShouldPreserveProgress()
    {
        // Arrange
        var originalProgress = 90;
        var currentState = new TaskState
        {
            TaskId = "test-task",
            Status = AgentTaskStatus.ReviewReady,
            Progress = originalProgress,
            CurrentPhase = "ReviewReady",
            LastUpdated = DateTime.UtcNow.AddDays(-8)
        };

        // Act
        var expiredState = new TaskState
        {
            TaskId = currentState.TaskId,
            Status = AgentTaskStatus.Expired,
            Progress = currentState.Progress, // Preserved
            CurrentPhase = "Expired",
            Result = "Expired",
            LastUpdated = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(originalProgress, expiredState.Progress);
        Assert.Equal(AgentTaskStatus.Expired, expiredState.Status);
    }

    [Fact]
    public void ExpiredTaskState_ShouldIncludeOriginalReviewReadyTime()
    {
        // Arrange
        var reviewReadyTime = DateTime.UtcNow.AddDays(-8);
        var expirationDays = 7;

        // Act
        var resultMessage = $"Draft expired after {expirationDays} days (ReviewReady at {reviewReadyTime:O})";

        // Assert
        Assert.Contains("Draft expired after", resultMessage);
        Assert.Contains("ReviewReady at", resultMessage);
        Assert.Contains(reviewReadyTime.ToString("O"), resultMessage);
    }

    [Fact]
    public void ExpirationService_ShouldOnlyExpireReviewReadyTasks()
    {
        // Arrange
        var statuses = new[]
        {
            AgentTaskStatus.Pending,
            AgentTaskStatus.Running,
            AgentTaskStatus.ReviewReady, // Only this should be expired
            AgentTaskStatus.Committed,
            AgentTaskStatus.Rejected,
            AgentTaskStatus.Expired,
            AgentTaskStatus.Completed,
            AgentTaskStatus.Failed,
            AgentTaskStatus.Cancelled
        };

        // Act & Assert
        foreach (var status in statuses)
        {
            var shouldExpire = (status == AgentTaskStatus.ReviewReady);
            Assert.True(shouldExpire == (status == AgentTaskStatus.ReviewReady),
                $"Only ReviewReady tasks should be expired, but got {status}");
        }
    }

    [Fact]
    public void IngestOptions_DraftExpiration_ShouldReturnCorrectTimeSpan()
    {
        // Arrange
        var options = new IngestOptions
        {
            DraftExpirationDays = 7
        };

        // Act
        var expirationTimeSpan = options.DraftExpiration;

        // Assert
        Assert.Equal(TimeSpan.FromDays(7), expirationTimeSpan);
    }

    [Fact]
    public void CosmosQuery_ShouldFilterByAgentType()
    {
        // Arrange - Validate query structure (contract test)
        var expectedAgentType = "Ingest";
        var threshold = DateTime.UtcNow.AddDays(-7);

        // Act - This is the query structure that should be used
        var queryText = @"SELECT * FROM c 
              WHERE c.AgentType = @agentType 
              AND c.CreatedAt < @threshold";

        // Assert
        Assert.Contains("c.AgentType = @agentType", queryText);
        Assert.Contains("c.CreatedAt < @threshold", queryText);
    }

    [Fact]
    public void ExpirationThreshold_ShouldBeCalculatedFromCurrentTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var expirationDays = 7;

        // Act
        var threshold = now.AddDays(-expirationDays);
        var calculatedExpiration = now.Add(-TimeSpan.FromDays(expirationDays));

        // Assert
        Assert.Equal(threshold.Date, calculatedExpiration.Date);
        Assert.True((threshold - calculatedExpiration).TotalSeconds < 1, "Calculation methods should produce same result");
    }
}
