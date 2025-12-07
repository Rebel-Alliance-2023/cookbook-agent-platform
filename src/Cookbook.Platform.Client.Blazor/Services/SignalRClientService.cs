using Cookbook.Platform.Shared.Messaging;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cookbook.Platform.Client.Blazor.Services;

/// <summary>
/// Service for managing SignalR connection to the agent hub.
/// </summary>
public class SignalRClientService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SignalRClientService> _logger;

    public event Action<AgentEvent>? OnEvent;
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public SignalRClientService(IConfiguration configuration, ILogger<SignalRClientService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        if (_hubConnection != null)
            return;

        var gatewayUrl = _configuration["services:gateway:http:0"] ?? "http://gateway";
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{gatewayUrl}/agentHub")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<AgentEvent>("ReceiveEvent", (agentEvent) =>
        {
            _logger.LogDebug("Received event: {EventType}", agentEvent.EventType);
            OnEvent?.Invoke(agentEvent);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR reconnecting");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
        }
    }

    public async Task JoinThreadAsync(string threadId)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            await ConnectAsync();
        }

        await _hubConnection!.InvokeAsync("JoinThread", threadId);
        _logger.LogInformation("Joined thread: {ThreadId}", threadId);
    }

    public async Task LeaveThreadAsync(string threadId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("LeaveThread", threadId);
            _logger.LogInformation("Left thread: {ThreadId}", threadId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
