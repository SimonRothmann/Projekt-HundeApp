namespace Dogity.Api.Contracts;

/// <param name="EmailBenachrichtigungen">
/// "Wichtiges auch per E-Mail" aus dem Registrierungsformular. Fehlt der Wert (eine
/// ältere App-Version im Cache kennt das Feld nicht), gilt die Vorgabe: ja.
/// </param>
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, bool? EmailBenachrichtigungen = null);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string RefreshToken, Guid UserId, string Email, string FirstName, string LastName, string[] Roles);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record RefreshTokenRequest(string RefreshToken);
