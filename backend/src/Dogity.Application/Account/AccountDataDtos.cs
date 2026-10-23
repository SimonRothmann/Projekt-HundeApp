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
    IReadOnlyList<UebungspruefungExportDto> Uebungspruefungen,
    IReadOnlyList<BenachrichtigungExportDto> Benachrichtigungen,
    IReadOnlyList<TerminzusageExportDto> Terminzusagen,
    IReadOnlyList<PushGeraetExportDto> PushGeraete,
    IReadOnlyList<TrainingspauseExportDto> Trainingspausen,
    IReadOnlyList<ErinnerungExportDto> ErinnerungenVersendet);

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
    IReadOnlyList<Guid> GewaehlteSportarten,
    bool WichtigesPerEMail = true,
    bool ErinnerungTermine = true,
    bool ErinnerungFaehrten = true,
    bool ErinnerungRueckmeldungen = true,
    IReadOnlyList<string>? RueckmeldungenWochentage = null,
    int RueckmeldungenUhrzeit = 18);

/// <summary>
/// Eine versendete Erinnerung - der Merkzettel gegen Doppelversand. Der Text steht
/// unter Benachrichtigungen; hier nur, wofür und wann.
/// </summary>
/// <param name="Art">"Termin am Vortag", "Fährte nicht abgelaufen" oder "Offene Rückmeldungen".</param>
/// <param name="Bezug">Id des Termins bzw. der Fährte; bei den offenen Rückmeldungen leer.</param>
/// <param name="Tag">Nur bei den offenen Rückmeldungen: der Tag, für den sie versendet wurde.</param>
public record ErinnerungExportDto(string Art, Guid? Bezug, DateOnly? Tag, DateTimeOffset VersendetAm);

/// <summary>
/// Ein Gerät, das Push-Benachrichtigungen bekommt. Bewusst nur Bezeichnung und
/// Zeitpunkte: Die Adresse beim Push-Dienst und die Schlüssel des Geräts sind
/// technische Zugangsdaten, keine Auskunft - wer sie hat, könnte Nachrichten
/// an das Gerät senden.
/// </summary>
public record PushGeraetExportDto(string? Geraet, DateTimeOffset AngemeldetAm, DateTimeOffset? ZuletztZugestellt);

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

/// <summary>
/// Eine Trainingspause eines meiner Hunde (auch der geteilten). Grund und
/// Notiz stehen drin: Für Besitzer:innen sind sie sichtbar, und es sind ihre
/// eigenen Angaben zum Hund.
/// </summary>
public record TrainingspauseExportDto(
    Guid HundId,
    string Grund,
    DateOnly Von,
    DateOnly? Bis,
    string? Notiz,
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
    IReadOnlyList<TrainingsuebungExportDto> Uebungen,
    // Die eigene Antwort auf die Trainer-Rückmeldung (Danke/Verstanden, Rückfrage).
    string? ReaktionAufRueckmeldung = null,
    string? RueckfrageZurRueckmeldung = null,
    DateTimeOffset? RueckfrageAm = null,
    // Wann die Trainer:in das Training als erledigt abgehakt hat.
    DateTimeOffset? VonTrainerAbgehaktAm = null);

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
    IReadOnlyList<PlanUebungExportDto> Planuebungen,
    // Prüfungsergebnis eines erreichten Ziels (Goal.ExamDate/ExamScore/ExamNote).
    DateOnly? Pruefungstag = null,
    int? Punkte = null,
    string? Pruefungsnotiz = null);

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
    bool IstTrainer,
    string Herkunft);

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

/// <summary>Ein gespeichertes Ergebnis einer Sachkunde-Übungsprüfung.</summary>
public record UebungspruefungExportDto(
    string Fragenkatalog,
    DateTimeOffset AbgegebenAm,
    int Punkte,
    int HoechstePunkte,
    bool Bestanden,
    int Fragen,
    int Richtig,
    IReadOnlyList<UebungspruefungKomplexExportDto> Komplexe);

public record UebungspruefungKomplexExportDto(string Komplex, int Punkte, int HoechstePunkte);

public record BenachrichtigungExportDto(
    DateTimeOffset Zeitpunkt,
    string Text,
    bool Gelesen);

/// <summary>Die eigene Zu- oder Absage zu einem Gruppentermin.</summary>
public record TerminzusageExportDto(
    Guid TerminId,
    string Gruppe,
    DateTimeOffset Beginn,
    bool Zugesagt,
    DateTimeOffset AntwortAm);
