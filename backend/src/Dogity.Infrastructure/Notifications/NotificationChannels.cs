using Dogity.Application.Notifications;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Push;
using Microsoft.Extensions.Options;

namespace Dogity.Infrastructure.Notifications;

/// <inheritdoc />
public class NotificationChannels(IOptions<SmtpSettings> smtp, IOptions<PushSettings> push) : INotificationChannels
{
    public bool EmailAktiv => smtp.Value.Aktiv;

    public string? PushPublicKey => push.Value.Aktiv ? push.Value.VapidPublicKey.Trim() : null;
}
