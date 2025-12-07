using System.Text.Json;
using Cookbook.Platform.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cookbook.Platform.Orchestrator.Services;

/// <summary>
/// Background service that processes tasks from the Redis stream.
/// </summary>
public class TaskProcessorService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly MessagingBusOptions _options;
    private readonly OrchestratorService _orchestrator;
    private readonly ILogger<TaskProcessorService> _logger;

    public TaskProcessorService(
        IConnectionMultiplexer redis,
        IOptions<MessagingBusOptions> options,
        OrchestratorService orchestrator,
        ILogger<TaskProcessorService> logger)
    {
        _redis = redis;
        _options = options.Value;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task processor service starting");

        var db = _redis.GetDatabase(_options.RedisDb);
        var consumerGroup = "orchestrator";
        var consumerName = Environment.MachineName;

        // Process tasks from Research and Analysis streams
        var streams = new[]
        {
            $"{_options.RedisStreamPrefix}Research",
            $"{_options.RedisStreamPrefix}Analysis"
        };

        foreach (var stream in streams)
        {
            try
            {
                await db.StreamCreateConsumerGroupAsync(stream, consumerGroup, "0-0", createStream: true);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // Consumer group already exists
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var stream in streams)
                {
                    var entries = await db.StreamReadGroupAsync(
                        stream,
                        consumerGroup,
                        consumerName,
                        count: 1,
                        noAck: false);

                    foreach (var entry in entries)
                    {
                        try
                        {
                            var payload = entry.Values.FirstOrDefault(v => v.Name == "task").Value;
                            
                            if (!payload.IsNullOrEmpty)
                            {
                                var task = JsonSerializer.Deserialize<AgentTask>((string)payload!);
                                
                                if (task != null)
                                {
                                    _logger.LogInformation("Processing task {TaskId} of type {AgentType}",
                                        task.TaskId, task.AgentType);

                                    await _orchestrator.ProcessTaskAsync(task, stoppingToken);

                                    // Acknowledge the message
                                    await db.StreamAcknowledgeAsync(stream, consumerGroup, entry.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing task from stream {Stream}", stream);
                        }
                    }
                }

                // Small delay between polling
                await Task.Delay(100, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in task processor loop");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
