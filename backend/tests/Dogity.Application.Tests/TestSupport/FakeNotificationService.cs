using Dogity.Application.Common;
using Dogity.Application.Notifications;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Erfasst erstellte Benachrichtigungen im Arbeitsspeicher - zum Prüfen,
/// ob Services die richtigen Benachrichtigungen auslösen.
/// </summary>
public class FakeNotificationService : INotificationService
{
    public List<(Guid UserId, string Message, string? LinkPath)> Created { get; } = [];

    /// <summary>Die Meldungen, die als "wichtig" (also zusätzlich per Mail) angelegt wurden.</summary>
    public List<(Guid UserId, string Message)> Wichtige { get; } = [];

    public Task CreateAsync(Guid userId, string message, string? linkPath = null, CancellationToken ct = default, bool wichtig = false)
    {
        Created.Add((userId, message, linkPath));
        if (wichtig) Wichtige.Add((userId, message));
        return Task.CompletedTask;
    }

    public Task<Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Result<IReadOnlyList<NotificationDto>>.Success((IReadOnlyList<NotificationDto>)[]));

    public Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Result<int>.Success(0));

    public Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
        => Task.FromResult(Result.Success());

    public Task<Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
