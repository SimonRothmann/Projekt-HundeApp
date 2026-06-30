using Dogity.Application.Common;

namespace Dogity.Application.Notifications;

/// <summary>
/// Erzeugt und verwaltet In-App-Benachrichtigungen. Wird von anderen
/// Application-Services als Nebeneffekt aufgerufen (z.B. ClubService nach
/// einer Beitritts-Entscheidung) - es gibt bewusst keinen
/// Controller-Endpoint zum direkten Erstellen, nur zum Lesen/Quittieren.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(Guid userId, string message, string? linkPath = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task<Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
