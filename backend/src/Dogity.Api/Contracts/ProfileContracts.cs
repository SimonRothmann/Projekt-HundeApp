namespace Dogity.Api.Contracts;

public record ProfileDto(string FirstName, string LastName, string Email, string? AvatarUrl);

public record UpdateProfileRequest(string FirstName, string LastName, string? AvatarUrl);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Kontolöschung nach Art. 17 DSGVO. Das Passwort ist Pflicht: Ein
/// unbeaufsichtigtes Telefon oder ein gestohlener Token darf nicht genügen,
/// um jemandem sein Tagebuch zu löschen - anders als bei allem anderen in
/// dieser App gibt es hier kein Zurück.
/// </summary>
public record DeleteAccountRequest(string CurrentPassword);
