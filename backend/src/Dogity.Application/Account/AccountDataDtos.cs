namespace Dogity.Application.Account;

/// <summary>
/// Der Datenexport nach Art. 15/20 DSGVO.
///
/// Die Feldnamen sind hier - anders als im Rest der API - deutsch. Das ist
/// kein Ausrutscher: Diese Datei ist die einzige Antwort der Schnittstelle,
/// die ein Mensch ohne Entwicklerkenntnisse liest. "Auskunft in verständlicher
/// Form" (Art. 12 Abs. 1) heißt bei einer JSON-Datei genau das - wer sie
/// öffnet, soll sehen, was über ihn gespeichert ist, ohne ein Wörterbuch.
/// </summary>
public record AccountExportDto(
    string Hinweis,
    DateTimeOffset ErstelltAm,
    KontoExportDto Konto,
    EinstellungenExportDto? Einstellungen,
    IReadOnlyList<HundExportDto> Hunde,
    IReadOnlyList<TrainingExportDto> Trainings,
    IReadOnlyList<FaehrteExportDto> Faehrten,
    IReadOnlyList<ZielExportDto> Ziele,
    IReadOnlyList<VereinExportDto> Vereine,
    IReadOnlyList<GruppeExportDto> Gruppen,
    IReadOnlyList<SachkundeExportDto> Sachkunde,
    IReadOnlyList<BenachrichtigungExportDto> Benachrichtigungen);

public record KontoExportDto(
    Guid Id,
    string Vorname,
    string Nachname,
    string EMail,
    string? AvatarUrl);

public record EinstellungenExportDto(
    string? Sprache,
    string? Land,
    string? Schriftgroesse,
    IReadOnlyList<string> AusgeblendeteModule,
    IReadOnlyList<Guid> GewaehlteSportarten);

public record HundExportDto(
    Guid Id,
    string Name,
    string? Rasse,
    DateOnly? Geburtstag,
    string Geschlecht,
    string? Notizen,
    string? BildUrl,
    bool HatHochgeladenesBild,
    DateTimeOffset? ArchiviertAm,
    string MeineRolle,
    DateTimeOffset AngelegtAm);

public record TrainingExportDto(
    Guid Id,
    Guid HundId,
    DateOnly Datum,
    TimeOnly? Uhrzeit,
    int DauerMinuten,
    string? Ort,
    double? Breitengrad,
    double? Laengengrad,
    double? TemperaturC,
    int? LuftfeuchteProzent,
    double? WindKmh,
    string? Notizen,
    string? TrainerRueckmeldung,
    IReadOnlyList<TrainingsuebungExportDto> Uebungen);

public record TrainingsuebungExportDto(
    string Uebung,
    int Bewertung,
    bool Gelungen,
    string? Notizen,
    int? TrainerBewertung,
    string? TrainerNotiz);

/// <summary>
/// Eine aufgezeichnete Fährte samt Einzelpunkten. Die Punkte sind der
/// datenschutzrechtlich empfindlichste Teil der App - sie zeigen auf wenige
/// Meter genau, wo jemand wann unterwegs war. Deshalb stehen sie vollständig
/// im Export und nicht nur als Zusammenfassung.
/// </summary>
public record FaehrteExportDto(
    Guid Id,
    Guid TrainingId,
    DateOnly Datum,
    double? LaengeMeter,
    int? AlterMinuten,
    string? Untergrund,
    string? Kommentar,
    IReadOnlyList<GpsPunktExportDto> Punkte,
    IReadOnlyList<AblaufExportDto> Ablaeufe);

public record GpsPunktExportDto(
    double Breitengrad,
    double Laengengrad,
    DateTimeOffset Zeitpunkt,
    double? GenauigkeitMeter,
    string Art,
    string? Beschriftung);

public record AblaufExportDto(
    Guid Id,
    double? LaengeMeter,
    double? MittlereAbweichungMeter,
    double? AufDerFaehrteProzent,
    int? GegenstaendeGefunden,
    IReadOnlyList<GpsPunktExportDto> Punkte);

public record ZielExportDto(
    Guid Id,
    Guid HundId,
    DateOnly Zieldatum,
    string Status,
    string? Notizen,
    DateTimeOffset? PlanErzeugtAm,
    IReadOnlyList<PlanUebungExportDto> Planuebungen);

public record PlanUebungExportDto(
    int Woche,
    int Tag,
    string Uebung,
    int Wiederholungen,
    bool Ruhewoche);

public record VereinExportDto(
    Guid VereinId,
    string Verein,
    string Status,
    DateTimeOffset BeantragtAm,
    DateTimeOffset? EntschiedenAm,
    bool IstTrainer);

public record GruppeExportDto(
    Guid GruppeId,
    string Gruppe,
    string Rolle,
    string Status,
    DateTimeOffset BeigetretenAm);

public record SachkundeExportDto(
    Guid FrageId,
    string Fragenkatalog,
    int Fach,
    int Richtig,
    int Falsch,
    DateTimeOffset? ZuletztBeantwortet);

public record BenachrichtigungExportDto(
    DateTimeOffset Zeitpunkt,
    string Text,
    bool Gelesen);
