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
    /// <summary>
    /// Legt die Benachrichtigung in der Glocke an und reiht danach Push (an alle
    /// Geräte der Person) und - bei <paramref name="wichtig"/> - eine Mail ein.
    /// Die Glocke ist die verlässliche Quelle: Scheitert Push oder Mail, wirft
    /// diese Methode nicht, die Meldung ist gespeichert.
    /// </summary>
    /// <param name="wichtig">
    /// Ob die Meldung zusätzlich per E-Mail kommt: Einladungen und Entscheidungen
    /// über Beitrittsanfragen, also alles, worauf jemand wartet oder wo er etwas
    /// tun soll. Alles andere (Feedback, Termine, Hinweise) bleibt Glocke und Push.
    /// Eine Mail geht nur, wenn die Person sie nicht abgeschaltet hat und der
    /// Mailversand eingerichtet ist. Am Ende der Parameter, damit bestehende
    /// Aufrufe mit <c>ct</c> an vierter Stelle unverändert bleiben; Aufrufer nennen ihn.
    /// </param>
    /// <param name="meldung">
    /// Vorlage und Werte, kein fertiger Text: Der Service schreibt den Satz in der
    /// Sprache der Empfänger:in (UserPreference.Locale) und verwendet denselben
    /// Text für Glocke, Mail und Push. Eine Überladung mit fertigem Text gibt es
    /// bewusst nicht - sie lüde dazu ein, wieder feste deutsche Sätze zu schreiben.
    /// </param>
    Task CreateAsync(Guid userId, Meldung meldung, string? linkPath = null, CancellationToken ct = default, bool wichtig = false);

    /// <summary>
    /// Ob die Person genau diese Meldung mit diesem Link schon ungelesen in der
    /// Glocke hat. Verglichen wird der Text in IHRER Sprache - der gespeicherte
    /// Satz ist schon übersetzt, ein Vergleich mit dem deutschen Text träfe bei
    /// englischen Empfänger:innen nie.
    /// </summary>
    Task<bool> HatUngelesenAsync(Guid userId, Meldung meldung, string linkPath, CancellationToken ct = default);

    Task<Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task<Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
