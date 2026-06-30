using Dogity.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine(CancellationToken ct)
    {
        var result = await notificationService.GetMyNotificationsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var result = await notificationService.GetUnreadCountAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await notificationService.MarkReadAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var result = await notificationService.MarkAllReadAsync(CurrentUserId, ct);
        return FromResult(result);
    }
}
