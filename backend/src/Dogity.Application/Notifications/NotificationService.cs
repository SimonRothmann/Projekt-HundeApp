using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Notifications;

public class NotificationService(IApplicationDbContext db) : INotificationService
{
    private const int MaxReturned = 50;

    public async Task CreateAsync(Guid userId, string message, string? linkPath = null, CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification { UserId = userId, Message = message, LinkPath = linkPath });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxReturned)
            .Select(n => new NotificationDto(n.Id, n.Message, n.LinkPath, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<NotificationDto>>.Success(notifications);
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return Result<int>.Success(count);
    }

    public async Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null)
            return Result.Failure("Benachrichtigung nicht gefunden.");

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return Result.Success();
    }
}
