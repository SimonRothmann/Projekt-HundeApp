namespace Dogity.Application.Notifications;

public record NotificationDto(Guid Id, string Message, string? LinkPath, bool IsRead, DateTimeOffset CreatedAt);
