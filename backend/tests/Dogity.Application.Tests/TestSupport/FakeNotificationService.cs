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

    /// <summary>Die Meldungen mit Vorlage und Werten, wie die Services sie übergeben haben.</summary>
    public List<(Guid UserId, Meldung Meldung)> Vorlagen { get; } = [];

    /// <summary>
    /// <see cref="Created"/> hält den deutschen Satz (wie ihn eine Person ohne gespeicherte
    /// Sprache lesen würde) - so prüfen die Tests den Wortlaut, ohne den NotificationService zu brauchen.
    /// </summary>
    public Task CreateAsync(Guid userId, Meldung meldung, string? linkPath = null, CancellationToken ct = default, bool wichtig = false)
    {
        var message = Uebersetzung.Rendere(meldung, null);
        Vorlagen.Add((userId, meldung));
        Created.Add((userId, message, linkPath));
        if (wichtig) Wichtige.Add((userId, message));
        return Task.CompletedTask;
    }

    /// <summary>Alles in <see cref="Created"/> gilt als ungelesen (der Fake kennt kein Lesen).</summary>
    public Task<bool> HatUngelesenAsync(Guid userId, Meldung meldung, string linkPath, CancellationToken ct = default)
    {
        var message = Uebersetzung.Rendere(meldung, null);
        return Task.FromResult(Created.Any(c => c.UserId == userId && c.Message == message && c.LinkPath == linkPath));
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
