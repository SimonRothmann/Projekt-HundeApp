using System.Data.Common;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;
using static Dogity.Application.Common.SqlStates;

namespace Dogity.Application.Community;

/// <summary>
/// Arbeitseinsätze des Vereins: Die Verwaltung trägt einen Einsatz ein und hakt ab, wer geholfen hat;
/// Verwaltung und Vorstand lesen die Jahresauswertung mit Namen, jedes Mitglied seine eigenen Einsätze.
/// Es gibt kein Soll, keine Selbsteintragung und keine Bestätigung - und bewusst keine Benachrichtigung
/// beim Abhaken: Transparenz entsteht über die eigene Ansicht, nicht über Lärm.
/// </summary>
public class ClubWorkService(IApplicationDbContext db, IUserLookupService userLookup, TimeProvider timeProvider) : IClubWorkService
{
    /// <summary>Kürzeste und längste Dauer in Minuten - für den Einsatz wie für abweichende Minuten einer Person.</summary>
    public const int MinMinuten = 15;
    public const int MaxMinuten = 720;

    /// <summary>So weit im Voraus darf ein Einsatz liegen (anlegen ja, abhaken erst am Tag selbst).</summary>
    public const int MaxTageVoraus = 30;

    // ---------- Lesen ----------

    public async Task<Result<IReadOnlyList<ClubWorkEventListItemDto>>> GetListAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.CanViewWorkEventsAsync(userId, clubId, ct))
            return Result<IReadOnlyList<ClubWorkEventListItemDto>>.NotFound("Verein nicht gefunden.");

        var einsaetze = await db.ClubWorkEvents
            .Where(e => e.ClubId == clubId)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        var ids = einsaetze.Select(e => e.Id).ToList();
        var teilnahmen = (await db.ClubWorkParticipations
                .Where(p => ids.Contains(p.ClubWorkEventId))
                .Select(p => new { p.ClubWorkEventId, p.Minutes })
                .ToListAsync(ct))
            .ToLookup(p => p.ClubWorkEventId);

        IReadOnlyList<ClubWorkEventListItemDto> liste = einsaetze
            .Select(e => new ClubWorkEventListItemDto(
                e.Id, e.Date, e.Title, e.DurationMinutes,
                teilnahmen[e.Id].Count(),
                teilnahmen[e.Id].Sum(p => p.Minutes ?? e.DurationMinutes)))
            .ToList();
        return Result<IReadOnlyList<ClubWorkEventListItemDto>>.Success(liste);
    }

    public async Task<Result<ClubWorkEventDto>> GetAsync(Guid userId, Guid clubId, Guid eventId, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: false, ct);
        if (einsatz is null) return Result<ClubWorkEventDto>.NotFound("Arbeitseinsatz nicht gefunden.");
        return Result<ClubWorkEventDto>.Success(await BaueAsync(einsatz, ct));
    }

    public async Task<Result<IReadOnlyList<ClubWorkCandidateDto>>> GetCandidatesAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.CanManageWorkEventsAsync(userId, clubId, ct))
            return Result<IReadOnlyList<ClubWorkCandidateDto>>.NotFound("Verein nicht gefunden.");

        var personen = await db.VereinsPersonenIds(clubId).ToListAsync(ct);
        var namen = await userLookup.FindByIdsAsync(personen, ct);

        IReadOnlyList<ClubWorkCandidateDto> liste = personen
            .Where(namen.ContainsKey)
            .Select(id => new ClubWorkCandidateDto(id, namen[id].FirstName, namen[id].LastName))
            .OrderBy(c => c.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Result<IReadOnlyList<ClubWorkCandidateDto>>.Success(liste);
    }

    public async Task<Result<IReadOnlyList<ClubWorkGuestDto>>> GetGuestsAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.CanManageWorkEventsAsync(userId, clubId, ct))
            return Result<IReadOnlyList<ClubWorkGuestDto>>.NotFound("Verein nicht gefunden.");

        var gaeste = await db.ClubWorkGuests.Where(g => g.ClubId == clubId).AsNoTracking().ToListAsync(ct);
        IReadOnlyList<ClubWorkGuestDto> liste = gaeste
            .Select(g => new ClubWorkGuestDto(g.Id, g.FirstName, g.LastName ?? ""))
            .OrderBy(g => g.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Result<IReadOnlyList<ClubWorkGuestDto>>.Success(liste);
    }

    public async Task<Result<ClubWorkSummaryDto>> GetSummaryAsync(Guid userId, Guid clubId, int? year, CancellationToken ct = default)
    {
        if (!await db.CanViewWorkEventsAsync(userId, clubId, ct))
            return Result<ClubWorkSummaryDto>.NotFound("Verein nicht gefunden.");
        if (year is < 2000 or > 2100) return Result<ClubWorkSummaryDto>.Failure("Bitte ein gültiges Jahr angeben.");

        var daten = await db.ClubWorkEvents.Where(e => e.ClubId == clubId).Select(e => e.Date).ToListAsync(ct);
        var jahre = daten.Select(d => d.Year).Distinct().OrderByDescending(j => j).ToList();
        // Ohne Angabe das neueste Jahr mit Einsätzen - der Einstieg zeigt etwas, sobald es etwas gibt.
        var jahr = year ?? jahre.FirstOrDefault(Heute().Year);

        var von = new DateOnly(jahr, 1, 1);
        var bis = new DateOnly(jahr, 12, 31);
        var einsaetze = await db.ClubWorkEvents
            .Where(e => e.ClubId == clubId && e.Date >= von && e.Date <= bis)
            .Select(e => new { e.Id, e.DurationMinutes })
            .AsNoTracking()
            .ToListAsync(ct);
        var dauer = einsaetze.ToDictionary(e => e.Id, e => e.DurationMinutes);
        var ids = dauer.Keys.ToList();
        var teilnahmen = (await db.ClubWorkParticipations
                .Where(p => ids.Contains(p.ClubWorkEventId))
                .Select(p => new { p.ClubWorkEventId, p.UserId, p.GuestId, p.Minutes })
                .ToListAsync(ct))
            .Select(p => new { p.ClubWorkEventId, p.UserId, p.GuestId, Minuten = p.Minutes ?? dauer[p.ClubWorkEventId] })
            .ToList();

        var namen = await userLookup.FindByIdsAsync(
            teilnahmen.Where(p => p.UserId is not null).Select(p => p.UserId!.Value).Distinct().ToList(), ct);
        var gaeste = await GaesteAsync(teilnahmen.Where(p => p.GuestId is not null).Select(p => p.GuestId!.Value).Distinct().ToList(), ct);
        // Wen es als Konto nicht mehr gibt, zählt wie ein ehemaliges Mitglied; Personen ohne Konto haben eigene Zeilen.
        bool Bekannt(Guid? id) => id is { } wer && namen.ContainsKey(wer);
        bool BekannterGast(Guid? id) => id is { } wer && gaeste.ContainsKey(wer);
        var bekannt = teilnahmen.Where(p => Bekannt(p.UserId)).ToList();
        var ehemalige = teilnahmen.Where(p => !Bekannt(p.UserId) && !BekannterGast(p.GuestId)).ToList();

        var zeilen = bekannt
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new ClubWorkSummaryRowDto(namen[g.Key].FirstName, namen[g.Key].LastName, g.Count(), g.Sum(p => p.Minuten)))
            .Concat(teilnahmen
                .Where(p => BekannterGast(p.GuestId))
                .GroupBy(p => p.GuestId!.Value)
                .Select(g => new ClubWorkSummaryRowDto(gaeste[g.Key].FirstName, gaeste[g.Key].LastName ?? "", g.Count(), g.Sum(p => p.Minuten), Guest: true)))
            .OrderByDescending(z => z.Minutes)
            .ThenBy(z => z.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(z => z.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result<ClubWorkSummaryDto>.Success(new ClubWorkSummaryDto(
            jahr,
            jahre,
            einsaetze.Count,
            teilnahmen.Count,
            teilnahmen.Sum(p => p.Minuten),
            zeilen,
            // Einsätze, nicht Teilnahmen: Zwei Ehemalige beim selben Einsatz sind ein Einsatz.
            ehemalige.Count == 0 ? null : new ClubWorkSummaryRowDto("", "", ehemalige.Select(p => p.ClubWorkEventId).Distinct().Count(), ehemalige.Sum(p => p.Minuten))));
    }

    public async Task<Result<ClubMyWorkDto>> GetMineAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        // Jedes Mitglied (und jede Rolle) des Vereins sieht die eigenen Einsätze - sonst niemand etwas.
        if (!await db.GehoertZumVereinAsync(userId, clubId, ct))
            return Result<ClubMyWorkDto>.NotFound("Verein nicht gefunden.");

        var zeilen = await db.ClubWorkParticipations
            .Where(p => p.UserId == userId && p.Event!.ClubId == clubId)
            .Select(p => new { p.Id, p.Event!.Date, p.Event.Title, Minuten = p.Minutes ?? p.Event.DurationMinutes })
            .AsNoTracking()
            .ToListAsync(ct);

        var jahr = Heute().Year;
        var imJahr = zeilen.Where(z => z.Date.Year == jahr).ToList();
        IReadOnlyList<ClubMyWorkItemDto> eintraege = zeilen
            .OrderByDescending(z => z.Date)
            .Select(z => new ClubMyWorkItemDto(z.Id, z.Date, z.Title, z.Minuten))
            .ToList();
        return Result<ClubMyWorkDto>.Success(new ClubMyWorkDto(jahr, imJahr.Count, imJahr.Sum(z => z.Minuten), eintraege));
    }

    // ---------- Einsatz ----------

    public async Task<Result<ClubWorkEventDto>> CreateAsync(Guid userId, Guid clubId, SaveClubWorkEventRequest request, CancellationToken ct = default)
    {
        if (!await db.CanManageWorkEventsAsync(userId, clubId, ct))
            return Result<ClubWorkEventDto>.NotFound("Verein nicht gefunden.");
        var fehler = Pruefe(request);
        if (fehler is not null) return Result<ClubWorkEventDto>.Failure(fehler);

        var einsatz = new ClubWorkEvent { ClubId = clubId, CreatedByUserId = userId };
        Uebernimm(einsatz, request);
        db.ClubWorkEvents.Add(einsatz);
        await db.SaveChangesAsync(ct);

        return Result<ClubWorkEventDto>.Success(await BaueAsync(einsatz, ct));
    }

    public async Task<Result<ClubWorkEventDto>> UpdateAsync(Guid userId, Guid clubId, Guid eventId, SaveClubWorkEventRequest request, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: true, ct, track: true);
        if (einsatz is null) return Result<ClubWorkEventDto>.NotFound("Arbeitseinsatz nicht gefunden.");
        var fehler = Pruefe(request);
        if (fehler is not null) return Result<ClubWorkEventDto>.Failure(fehler);

        // Abgehakt wird erst ab dem Tag selbst - ein Datum in der Zukunft ließe die Haken nachträglich zu früh stehen.
        if (request.Date > Heute() && await db.ClubWorkParticipations.AnyAsync(p => p.ClubWorkEventId == einsatz.Id, ct))
            return Result<ClubWorkEventDto>.Failure("Bei dem Einsatz sind schon Helfer:innen abgehakt - das Datum kann nicht in die Zukunft liegen.");

        Uebernimm(einsatz, request);
        einsatz.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Result<ClubWorkEventDto>.Success(await BaueAsync(einsatz, ct));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid clubId, Guid eventId, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: true, ct, track: true);
        if (einsatz is null) return Result.NotFound("Arbeitseinsatz nicht gefunden.");

        // Die Teilnahmen sind Angaben zu Personen: Sie gehen endgültig mit, nicht erst im Papierkorb der Tabelle.
        db.ClubWorkParticipations.RemoveRange(
            await db.ClubWorkParticipations.IgnoreQueryFilters().Where(p => p.ClubWorkEventId == einsatz.Id).ToListAsync(ct));
        einsatz.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------- Helfer:innen ----------

    public async Task<Result> AddParticipantsAsync(Guid userId, Guid clubId, Guid eventId, AddClubWorkParticipantsRequest request, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: true, ct);
        if (einsatz is null) return Result.NotFound("Arbeitseinsatz nicht gefunden.");
        if (einsatz.Date > Heute()) return Result.Failure("Helfer:innen lassen sich erst ab dem Tag des Einsatzes abhaken.");

        var gewollt = (request.UserIds ?? []).Distinct().ToList();
        var gewollteGaeste = (request.GuestIds ?? []).Distinct().ToList();
        if (gewollt.Count == 0 && gewollteGaeste.Count == 0) return Result.Failure("Bitte wähle mindestens eine Person.");
        var imVerein = await db.VereinsPersonenIds(clubId).Where(id => gewollt.Contains(id)).CountAsync(ct);
        if (imVerein != gewollt.Count) return Result.Failure("Diese Person gehört nicht zum Verein.");
        var gaesteImVerein = await db.ClubWorkGuests.Where(g => g.ClubId == clubId && gewollteGaeste.Contains(g.Id)).CountAsync(ct);
        if (gaesteImVerein != gewollteGaeste.Count) return Result.Failure("Diese Person gehört nicht zum Verein.");

        var vorhandene = await db.ClubWorkParticipations
            .Where(p => p.ClubWorkEventId == einsatz.Id)
            .Select(p => new { p.UserId, p.GuestId })
            .ToListAsync(ct);
        var schonDa = vorhandene.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToList();
        var gastSchonDa = vorhandene.Where(p => p.GuestId != null).Select(p => p.GuestId!.Value).ToList();

        // Je Person ein eigenes Speichern: Gleichzeitige Anfragen (Doppeltipp, zwei Personen der Verwaltung)
        // können sich so nicht gegenseitig verklemmen - mehrere Zeilen in einem Speichern sperren in
        // wechselnder Reihenfolge, und Postgres bricht dann eine Anfrage ab. Wer inzwischen von der anderen
        // Anfrage abgehakt wurde, scheitert am eindeutigen Index; das ist kein Fehler, die Person steht ja da.
        foreach (var id in gewollt.Except(schonDa))
            await HakeAbAsync(new ClubWorkParticipation { ClubWorkEventId = einsatz.Id, UserId = id }, userId, ct);
        foreach (var id in gewollteGaeste.Except(gastSchonDa))
            await HakeAbAsync(new ClubWorkParticipation { ClubWorkEventId = einsatz.Id, GuestId = id }, userId, ct);
        return Result.Success();
    }

    private async Task HakeAbAsync(ClubWorkParticipation teilnahme, Guid userId, CancellationToken ct)
    {
        teilnahme.RecordedByUserId = userId;
        teilnahme.RecordedAt = timeProvider.GetUtcNow();
        db.ClubWorkParticipations.Add(teilnahme);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IstGleichzeitigerKonflikt(ex) || IstInzwischenWeg(ex))
        {
            // Schon abgehakt, oder die Person ohne Konto wurde gleichzeitig gelöscht bzw. zugeordnet:
            // dann gibt es nichts mehr abzuhaken.
            db.ClubWorkParticipations.Remove(teilnahme);
        }
    }

    // ---------- Helfer:innen ohne Konto ----------

    public async Task<Result<ClubWorkGuestDto>> CreateGuestAsync(Guid userId, Guid clubId, SaveClubWorkGuestRequest request, CancellationToken ct = default)
    {
        if (!await db.CanManageWorkEventsAsync(userId, clubId, ct))
            return Result<ClubWorkGuestDto>.NotFound("Verein nicht gefunden.");
        var fehler = PruefeGast(request);
        if (fehler is not null) return Result<ClubWorkGuestDto>.Failure(fehler);

        var gast = new ClubWorkGuest { ClubId = clubId, CreatedByUserId = userId };
        UebernimmGast(gast, request);
        db.ClubWorkGuests.Add(gast);
        await db.SaveChangesAsync(ct);
        return Result<ClubWorkGuestDto>.Success(new ClubWorkGuestDto(gast.Id, gast.FirstName, gast.LastName ?? ""));
    }

    public async Task<Result<ClubWorkGuestDto>> UpdateGuestAsync(Guid userId, Guid clubId, Guid guestId, SaveClubWorkGuestRequest request, CancellationToken ct = default)
    {
        var gast = await LadeGastAsync(userId, clubId, guestId, ct);
        if (gast is null) return Result<ClubWorkGuestDto>.NotFound("Person nicht gefunden.");
        var fehler = PruefeGast(request);
        if (fehler is not null) return Result<ClubWorkGuestDto>.Failure(fehler);

        UebernimmGast(gast, request);
        gast.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result<ClubWorkGuestDto>.Success(new ClubWorkGuestDto(gast.Id, gast.FirstName, gast.LastName ?? ""));
    }

    public async Task<Result> DeleteGuestAsync(Guid userId, Guid clubId, Guid guestId, CancellationToken ct = default)
    {
        var gast = await LadeGastAsync(userId, clubId, guestId, ct);
        if (gast is null) return Result.NotFound("Person nicht gefunden.");

        // Endgültig, samt Teilnahmen: Es sind Angaben zu einer Person ohne eigenes Konto, die der Verein auf Wunsch entfernen können muss.
        db.ClubWorkParticipations.RemoveRange(
            await db.ClubWorkParticipations.IgnoreQueryFilters().Where(p => p.GuestId == gast.Id).ToListAsync(ct));
        db.ClubWorkGuests.Remove(gast);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AssignGuestAsync(Guid userId, Guid clubId, Guid guestId, AssignClubWorkGuestRequest request, CancellationToken ct = default)
    {
        var gast = await LadeGastAsync(userId, clubId, guestId, ct);
        if (gast is null) return Result.NotFound("Person nicht gefunden.");
        if (!await db.VereinsPersonenIds(clubId).AnyAsync(id => id == request.UserId, ct))
            return Result.Failure("Diese Person gehört nicht zum Verein.");

        var vonGast = await db.ClubWorkParticipations.Where(p => p.GuestId == gast.Id).ToListAsync(ct);
        var einsatzIds = vonGast.Select(p => p.ClubWorkEventId).ToList();
        var dauer = await db.ClubWorkEvents.Where(e => einsatzIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DurationMinutes, ct);
        var vomMitglied = (await db.ClubWorkParticipations
                .Where(p => p.UserId == request.UserId && einsatzIds.Contains(p.ClubWorkEventId))
                .ToListAsync(ct))
            .ToDictionary(p => p.ClubWorkEventId);

        foreach (var teilnahme in vonGast)
        {
            if (!vomMitglied.TryGetValue(teilnahme.ClubWorkEventId, out var vorhanden))
            {
                teilnahme.GuestId = null;
                teilnahme.UserId = request.UserId;
                teilnahme.UpdatedAt = timeProvider.GetUtcNow();
                continue;
            }
            // Das Mitglied ist bei diesem Einsatz schon abgehakt: Es bleibt die Teilnahme mit den höheren Minuten.
            var einsatzDauer = dauer.GetValueOrDefault(teilnahme.ClubWorkEventId);
            if ((teilnahme.Minutes ?? einsatzDauer) > (vorhanden.Minutes ?? einsatzDauer))
            {
                vorhanden.Minutes = teilnahme.Minutes;
                vorhanden.UpdatedAt = timeProvider.GetUtcNow();
            }
            db.ClubWorkParticipations.Remove(teilnahme);
        }
        db.ClubWorkGuests.Remove(gast);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IstGleichzeitigerKonflikt(ex))
        {
            // Zwischendurch wurde das Mitglied von einer anderen Anfrage abgehakt; ein neuer Versuch sieht es.
            return Result.Failure("Das Zuordnen hat sich mit einer anderen Änderung überschnitten. Bitte versuche es noch einmal.");
        }
        return Result.Success();
    }

    public async Task<Result> SetMinutesAsync(Guid userId, Guid clubId, Guid eventId, Guid participationId, SetClubWorkMinutesRequest request, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: true, ct);
        if (einsatz is null) return Result.NotFound("Arbeitseinsatz nicht gefunden.");
        var teilnahme = await db.ClubWorkParticipations.FirstOrDefaultAsync(p => p.Id == participationId && p.ClubWorkEventId == einsatz.Id, ct);
        if (teilnahme is null) return Result.NotFound("Teilnahme nicht gefunden.");
        if (request.Minutes is < MinMinuten or > MaxMinuten)
            return Result.Failure($"Die Dauer muss zwischen {MinMinuten} und {MaxMinuten} Minuten liegen.");

        // Gleich der Dauer des Einsatzes heißt "wie alle": So folgt die Person einer späteren Änderung der Dauer.
        teilnahme.Minutes = request.Minutes == einsatz.DurationMinutes ? null : request.Minutes;
        teilnahme.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveParticipantAsync(Guid userId, Guid clubId, Guid eventId, Guid participationId, CancellationToken ct = default)
    {
        var einsatz = await LadeAsync(userId, clubId, eventId, schreiben: true, ct);
        if (einsatz is null) return Result.NotFound("Arbeitseinsatz nicht gefunden.");
        var teilnahme = await db.ClubWorkParticipations.FirstOrDefaultAsync(p => p.Id == participationId && p.ClubWorkEventId == einsatz.Id, ct);
        if (teilnahme is null) return Result.NotFound("Teilnahme nicht gefunden.");

        // Endgültig: Wer auf Widerspruch ausgetragen wird, soll in der Tabelle nicht weiter stehen.
        db.ClubWorkParticipations.Remove(teilnahme);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------- Hilfen ----------

    /// <summary>
    /// Ob ein Speichern an einer gleichzeitigen Anfrage gescheitert ist: eindeutiger Index oder Verklemmung.
    /// Die Datenbankmeldung kann tiefer stecken - die Wiederholungsstrategie von EF verpackt eine Verklemmung
    /// in eine InvalidOperationException.
    /// </summary>
    private static bool IstGleichzeitigerKonflikt(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is DbException { SqlState: EindeutigerIndexVerletzt or Verklemmung })
                return true;
        return false;
    }

    private static bool IstInzwischenWeg(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is DbException { SqlState: FremdschluesselVerletzt })
                return true;
        return false;
    }

    private DateOnly Heute() => Vereinszeit.Tag(timeProvider.GetUtcNow());

    /// <summary>Der Einsatz, wenn die Person ihn lesen (oder mit <paramref name="schreiben"/> ändern) darf - sonst null (fremder Verein, fehlende Rolle, gibt es nicht).</summary>
    private async Task<ClubWorkEvent?> LadeAsync(Guid userId, Guid clubId, Guid eventId, bool schreiben, CancellationToken ct, bool track = false)
    {
        var darf = schreiben ? await db.CanManageWorkEventsAsync(userId, clubId, ct) : await db.CanViewWorkEventsAsync(userId, clubId, ct);
        if (!darf) return null;
        var abfrage = db.ClubWorkEvents.Where(e => e.Id == eventId && e.ClubId == clubId);
        return await (track ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    /// <summary>Die Person ohne Konto, wenn die Person sie ändern darf (Verwaltung des Vereins) - sonst null.</summary>
    private async Task<ClubWorkGuest?> LadeGastAsync(Guid userId, Guid clubId, Guid guestId, CancellationToken ct)
    {
        if (!await db.CanManageWorkEventsAsync(userId, clubId, ct)) return null;
        return await db.ClubWorkGuests.FirstOrDefaultAsync(g => g.Id == guestId && g.ClubId == clubId, ct);
    }

    private async Task<Dictionary<Guid, ClubWorkGuest>> GaesteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? []
            : await db.ClubWorkGuests.Where(g => ids.Contains(g.Id)).AsNoTracking().ToDictionaryAsync(g => g.Id, ct);

    private static string? PruefeGast(SaveClubWorkGuestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName)) return "Bitte einen Vornamen angeben.";
        return Textlaengen.ZuLang(request.FirstName, Textlaengen.ArbeitseinsatzGastName, "Der Vorname")
            ?? Textlaengen.ZuLang(request.LastName, Textlaengen.ArbeitseinsatzGastName, "Der Nachname");
    }

    private static void UebernimmGast(ClubWorkGuest gast, SaveClubWorkGuestRequest request)
    {
        gast.FirstName = request.FirstName.Trim();
        gast.LastName = Texte.NullIfBlank(request.LastName);
    }

    private string? Pruefe(SaveClubWorkEventRequest request)
    {
        if (request.Date == default) return "Bitte das Datum des Einsatzes angeben.";
        var heute = Heute();
        if (request.Date > heute.AddDays(MaxTageVoraus))
            return $"Ein Arbeitseinsatz lässt sich höchstens {MaxTageVoraus} Tage im Voraus anlegen.";
        // Ältere Einsätze löschte der tägliche Lauf gleich wieder.
        var aeltestes = ClubWorkRetention.Grenze(heute).Year;
        if (request.Date < ClubWorkRetention.Grenze(heute))
            return $"Einsätze vor dem Jahr {aeltestes} werden nicht aufbewahrt.";

        if (string.IsNullOrWhiteSpace(request.Title)) return "Bitte einen Titel angeben.";
        var zuLang = Textlaengen.ZuLang(request.Title, Textlaengen.ArbeitseinsatzTitel, "Der Titel")
            ?? Textlaengen.ZuLang(request.Note, Textlaengen.ArbeitseinsatzNotiz, "Die Notiz");
        if (zuLang is not null) return zuLang;

        if (request.DurationMinutes is < MinMinuten or > MaxMinuten)
            return $"Die Dauer muss zwischen {MinMinuten} und {MaxMinuten} Minuten liegen.";
        return null;
    }

    private static void Uebernimm(ClubWorkEvent einsatz, SaveClubWorkEventRequest request)
    {
        einsatz.Date = request.Date;
        einsatz.Title = request.Title.Trim();
        einsatz.Note = Texte.NullIfBlank(request.Note);
        einsatz.DurationMinutes = request.DurationMinutes;
    }

    private async Task<ClubWorkEventDto> BaueAsync(ClubWorkEvent einsatz, CancellationToken ct)
    {
        var vereinsName = await db.Clubs.Where(c => c.Id == einsatz.ClubId).Select(c => c.Name).FirstAsync(ct);
        var teilnahmen = await db.ClubWorkParticipations
            .Where(p => p.ClubWorkEventId == einsatz.Id)
            .AsNoTracking()
            .ToListAsync(ct);
        var namen = await userLookup.FindByIdsAsync(
            teilnahmen.Where(p => p.UserId is not null).Select(p => p.UserId!.Value).Distinct().ToList(), ct);
        var gaeste = await GaesteAsync(teilnahmen.Where(p => p.GuestId is not null).Select(p => p.GuestId!.Value).Distinct().ToList(), ct);

        var helfer = teilnahmen
            .Select(p =>
            {
                var minuten = p.Minutes ?? einsatz.DurationMinutes;
                if (p.GuestId is { } gastId && gaeste.TryGetValue(gastId, out var gast))
                    return new ClubWorkParticipantDto(p.Id, null, gast.FirstName, gast.LastName ?? "", false, minuten, p.Minutes is not null, gastId, true);
                var info = p.UserId is { } id && namen.TryGetValue(id, out var gefunden) ? gefunden : null;
                return new ClubWorkParticipantDto(
                    p.Id, info is null ? null : p.UserId, info?.FirstName ?? "", info?.LastName ?? "", info is null,
                    minuten, p.Minutes is not null);
            })
            // Ehemalige Mitglieder ans Ende, die übrigen nach Namen.
            .OrderBy(h => h.Former)
            .ThenBy(h => h.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(h => h.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ClubWorkEventDto(
            einsatz.Id, einsatz.ClubId, vereinsName, einsatz.Date, einsatz.Title, einsatz.Note, einsatz.DurationMinutes, helfer);
    }
}
