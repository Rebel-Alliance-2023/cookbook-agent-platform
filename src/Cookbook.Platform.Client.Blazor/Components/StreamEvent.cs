namespace Cookbook.Platform.Client.Blazor.Components;

/// <summary>
/// Represents an event in the stream viewer.
/// </summary>
public record StreamEvent
{
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
