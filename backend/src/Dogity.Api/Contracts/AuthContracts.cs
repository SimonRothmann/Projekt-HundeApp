namespace Dogity.Api.Contracts;

/// <param name="EmailBenachrichtigungen">
/// "Wichtiges auch per E-Mail" aus dem Registrierungsformular. Fehlt der Wert (eine
/// ältere App-Version im Cache kennt das Feld nicht), gilt die Vorgabe: ja.
/// </param>
/// <param name="Locale">
/// Die Oberflächensprache beim Registrieren ("de"/"en"). Sie gilt für Mails und Push,
/// bis die Person sie im Profil ändert. Fehlt sie oder ist unbekannt: nichts wird gespeichert.
/// </param>
/// <param name="FromGuest">
/// Die Registrierung kam aus dem Gast-Banner ("Eigenes Konto anlegen"). Sie wird dann nur gezählt
/// (Admin-Statistik "App ausprobieren"); an der Registrierung selbst ändert das nichts.
/// </param>
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, bool? EmailBenachrichtigungen = null, string? Locale = null, bool FromGuest = false);
/// <param name="Locale">Die Oberflächensprache ("de"/"en"), wie bei <see cref="RegisterRequest"/>.</param>
public record GuestRequest(string? Locale = null);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string RefreshToken, Guid UserId, string Email, string FirstName, string LastName, string[] Roles);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record RefreshTokenRequest(string RefreshToken);
