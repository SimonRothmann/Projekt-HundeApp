using Dogity.Domain.Common;

namespace Dogity.Domain.Notifications;

/// <summary>
/// In-App-Benachrichtigung für einen Nutzer (z.B. "Beitritt freigegeben",
/// "zum Trainer befördert", "Trainer hat Feedback hinterlassen"). Bewusst
/// ein eigener, von Community/Training entkoppelter Namespace, da
/// Benachrichtigungen aus mehreren Modulen entstehen können - kein FK auf
/// die auslösende Entität, nur ein optionaler Klick-Ziel-Pfad
/// (<see cref="LinkPath"/>).
/// </summary>
public class Notification : Entity
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? LinkPath { get; set; }
    public bool IsRead { get; set; }
}
