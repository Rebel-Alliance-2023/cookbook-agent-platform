namespace Cookbook.Platform.Shared.Messaging;

/// <summary>
/// Configuration options for the messaging bus.
/// </summary>
public class MessagingBusOptions
{
    public const string SectionName = "MessagingBus";

    /// <summary>
    /// The messaging bus implementation to use (RedisSignalR or AzureServiceBus).
    /// </summary>
    public string Implementation { get; set; } = "RedisSignalR";

    /// <summary>
    /// Redis connection string (used when Implementation is RedisSignalR).
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Prefix for Redis streams.
    /// </summary>
    public string RedisStreamPrefix { get; set; } = "stream:";

    /// <summary>
    /// Prefix for Redis keys.
    /// </summary>
    public string RedisKeyPrefix { get; set; } = "runs:";

    /// <summary>
    /// Redis database number.
    /// </summary>
    public int RedisDb { get; set; } = 0;

    /// <summary>
    /// Default TTL for semi-durable state in minutes.
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Azure Service Bus connection string (used when Implementation is AzureServiceBus).
    /// </summary>
    public string? ServiceBusConnectionString { get; set; }
}
