using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Messaging;
using Cookbook.Platform.Shared.Models.Ingest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for IngestPhaseRunner.
/// </summary>
public class IngestPhaseRunnerTests
{
    private readonly Mock<IMessagingBus> _messagingBusMock;
    private readonly Mock<ILogger<IngestPhaseRunner>> _loggerMock;
    private readonly IngestPhaseRunner _runner;

    public IngestPhaseRunnerTests()
    {
        _messagingBusMock = new Mock<IMessagingBus>();
        _loggerMock = new Mock<ILogger<IngestPhaseRunner>>();
        _runner = new IngestPhaseRunner(_messagingBusMock.Object, _loggerMock.Object);
    }

    #region Phase Constants Tests

    [Fact]
    public void IngestPhases_HasCorrectPhaseNames()
    {
        Assert.Equal("Ingest.Fetch", IngestPhases.Fetch);
        Assert.Equal("Ingest.Extract", IngestPhases.Extract);
        Assert.Equal("Ingest.Validate", IngestPhases.Validate);
        Assert.Equal("Ingest.ReviewReady", IngestPhases.ReviewReady);
    }

    [Fact]
    public void IngestPhases_Weights_SumTo100()
    {
        var total = IngestPhases.Weights.Fetch 
            + IngestPhases.Weights.Extract 
            + IngestPhases.Weights.Validate 
            + IngestPhases.Weights.ReviewReady
            + IngestPhases.Weights.Finalize;
        
        Assert.Equal(100, total);
    }

    [Theory]
    [InlineData(IngestPhases.Fetch, 100, 15)]
    [InlineData(IngestPhases.Extract, 100, 55)] // 15 + 40
    [InlineData(IngestPhases.Validate, 100, 80)] // 15 + 40 + 25
    [InlineData(IngestPhases.ReviewReady, 100, 90)] // 15 + 40 + 25 + 10
    public void CalculateProgress_ReturnsCorrectCumulativeProgress(string phase, int phaseProgress, int expected)
    {
        var result = IngestPhaseRunner.CalculateProgress(phase, phaseProgress);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(IngestPhases.Fetch, 50, 7)] // 15 * 0.5 = 7.5 -> 7
    [InlineData(IngestPhases.Extract, 50, 35)] // 15 + (40 * 0.5) = 35
    public void CalculateProgress_HandlesPartialPhaseProgress(string phase, int phaseProgress, int expected)
    {
        var result = IngestPhaseRunner.CalculateProgress(phase, phaseProgress);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Pipeline Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithValidUrlPayload_ReturnsSuccess()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var task = CreateTestTask(payload);

        // Act
        var result = await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Draft);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJsonPayload_ReturnsError()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            ThreadId = Guid.NewGuid().ToString(),
            AgentType = "Ingest",
            Payload = "not valid json {{{",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("INVALID_PAYLOAD", result.ErrorCode);
        Assert.Equal("Initialization", result.FailedPhase);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesProgressAtEachPhase()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var task = CreateTestTask(payload);
        var progressUpdates = new List<(string Phase, int Progress)>();

        _messagingBusMock
            .Setup(x => x.SetTaskStateAsync(
                It.IsAny<string>(),
                It.IsAny<TaskState>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TaskState, TimeSpan?, CancellationToken>((id, state, ttl, ct) =>
            {
                if (state.CurrentPhase != null)
                    progressUpdates.Add((state.CurrentPhase, state.Progress));
            })
            .Returns(Task.CompletedTask);

        // Act
        await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert - should have progress updates for all phases
        Assert.Contains(progressUpdates, p => p.Phase == IngestPhases.Fetch);
        Assert.Contains(progressUpdates, p => p.Phase == IngestPhases.Extract);
        Assert.Contains(progressUpdates, p => p.Phase == IngestPhases.Validate);
        Assert.Contains(progressUpdates, p => p.Phase == IngestPhases.ReviewReady);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesProgressEvents()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var task = CreateTestTask(payload);
        var publishedEvents = new List<AgentEvent>();

        _messagingBusMock
            .Setup(x => x.PublishEventAsync(
                It.IsAny<string>(),
                It.IsAny<AgentEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, AgentEvent, CancellationToken>((threadId, evt, ct) =>
            {
                publishedEvents.Add(evt);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert - should have progress events
        Assert.True(publishedEvents.Count >= 4); // At least one per phase
        Assert.All(publishedEvents, e => Assert.Equal("ingest.progress", e.EventType));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var task = CreateTestTask(payload);
        var cts = new CancellationTokenSource();
        
        // Setup messaging bus to throw on cancellation
        _messagingBusMock
            .Setup(x => x.SetTaskStateAsync(
                It.IsAny<string>(),
                It.IsAny<TaskState>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string id, TaskState state, TimeSpan? ttl, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _runner.ExecuteAsync(task, cts.Token));
    }

    #endregion

    #region Draft Creation Tests

    [Fact]
    public async Task ExecuteAsync_CreatesDraftWithSourceInfo()
    {
        // Arrange
        var url = "https://example.com/recipe/test";
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = url
        };

        var task = CreateTestTask(payload);

        // Act
        var result = await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Draft);
        Assert.NotNull(result.Draft.Source);
        Assert.Equal(url, result.Draft.Source.Url);
        Assert.True(result.Draft.Source.RetrievedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDraftWithValidation()
    {
        // Arrange
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe"
        };

        var task = CreateTestTask(payload);

        // Act
        var result = await _runner.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Draft);
        Assert.NotNull(result.Draft.ValidationReport);
    }

    #endregion

    #region Helper Methods

    private static AgentTask CreateTestTask(IngestPayload payload)
    {
        return new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            ThreadId = Guid.NewGuid().ToString(),
            AgentType = "Ingest",
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

/// <summary>
/// Tests for IngestPipelineException.
/// </summary>
public class IngestPipelineExceptionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var exception = new IngestPipelineException(
            "Test error message",
            "TEST_ERROR",
            "Ingest.Fetch");

        // Assert
        Assert.Equal("Test error message", exception.Message);
        Assert.Equal("TEST_ERROR", exception.ErrorCode);
        Assert.Equal("Ingest.Fetch", exception.Phase);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsAllProperties()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner error");

        // Act
        var exception = new IngestPipelineException(
            "Outer error",
            "OUTER_ERROR",
            "Ingest.Extract",
            inner);

        // Assert
        Assert.Equal("Outer error", exception.Message);
        Assert.Equal("OUTER_ERROR", exception.ErrorCode);
        Assert.Equal("Ingest.Extract", exception.Phase);
        Assert.Same(inner, exception.InnerException);
    }
}
