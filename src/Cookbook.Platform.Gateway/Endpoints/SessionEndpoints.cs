using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Storage.Repositories;

namespace Cookbook.Platform.Gateway.Endpoints;

/// <summary>
/// Session management endpoints.
/// </summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sessions")
            .WithTags("Sessions");

        group.MapPost("/", CreateSession)
            .WithName("CreateSession")
            .WithSummary("Creates a new session");

        group.MapGet("/{threadId}", GetSession)
            .WithName("GetSession")
            .WithSummary("Gets a session by thread ID");

        group.MapGet("/{threadId}/messages", GetMessages)
            .WithName("GetMessages")
            .WithSummary("Gets messages for a session");

        return endpoints;
    }

    private static async Task<IResult> CreateSession(
        SessionRepository sessionRepository,
        CancellationToken cancellationToken)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString(),
            ThreadId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Status = SessionStatus.Active
        };

        await sessionRepository.CreateAsync(session, cancellationToken);
        return Results.Created($"/api/sessions/{session.ThreadId}", session);
    }

    private static async Task<IResult> GetSession(
        string threadId,
        SessionRepository sessionRepository,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByThreadIdAsync(threadId, cancellationToken);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> GetMessages(
        string threadId,
        MessageRepository messageRepository,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var messages = await messageRepository.GetByThreadIdAsync(threadId, limit, cancellationToken);
        return Results.Ok(messages);
    }
}
