using Cookbook.Platform.Shared.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Infrastructure.Messaging;

/// <summary>
/// SignalR hub for agent communications at /agentHub.
/// Manages per-thread groups for real-time event streaming.
/// </summary>
public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Joins a client to a thread group for receiving events.
    /// </summary>
    public async Task JoinThread(string threadId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, threadId);
        _logger.LogInformation("Client {ConnectionId} joined thread {ThreadId}", Context.ConnectionId, threadId);
    }

    /// <summary>
    /// Removes a client from a thread group.
    /// </summary>
    public async Task LeaveThread(string threadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, threadId);
        _logger.LogInformation("Client {ConnectionId} left thread {ThreadId}", Context.ConnectionId, threadId);
    }

    /// <summary>
    /// Sends a user message to the system.
    /// </summary>
    public async Task SendMessage(string threadId, string message)
    {
        _logger.LogDebug("Received message from client {ConnectionId} in thread {ThreadId}", Context.ConnectionId, threadId);
        
        // Broadcast to other clients in the same thread
        await Clients.OthersInGroup(threadId).SendAsync("ReceiveMessage", new
        {
            ThreadId = threadId,
            Role = "user",
            Content = message,
            Timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
