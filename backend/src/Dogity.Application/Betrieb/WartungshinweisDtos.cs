namespace Dogity.Application.Betrieb;

/// <summary>Was alle sehen (auch ohne Anmeldung), solange ein Hinweis anzuzeigen ist.</summary>
/// <param name="Text">Eigener Text des Betreibers; null = das Frontend nimmt seinen Standardtext.</param>
/// <param name="Laeuft">Die Wartung hat begonnen (Beginn erreicht).</param>
/// <param name="GeaendertAm">Schlüssel fürs Wegklicken im Frontend.</param>
public record WartungshinweisDto(string? Text, DateTimeOffset Beginn, DateTimeOffset? Ende, bool Laeuft, DateTimeOffset GeaendertAm);

/// <summary>Der gespeicherte Hinweis für die Verwaltung; alles null/aus, solange noch nie einer gesetzt wurde.</summary>
public record WartungshinweisAdminDto(string? Text, DateTimeOffset? Beginn, DateTimeOffset? Ende, bool Aktiv, DateTimeOffset? GeaendertAm);

public record SetWartungshinweisRequest(string? Text, DateTimeOffset Beginn, DateTimeOffset? Ende, bool Aktiv);
