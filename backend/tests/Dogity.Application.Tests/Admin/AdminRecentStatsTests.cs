using Dogity.Application.Account;
using Dogity.Application.Admin;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// "Die letzten 30 Tage" der Admin-Übersicht: Zählungen über neue Konten und
/// aktive Konten - mit den Randfällen, an denen solche Zahlen sonst
/// schöngerechnet werden (gelöschter Hund, abgelehnte Mitgliedschaft, alte
/// Konten, doppelte Zeilen).
/// </summary>
public class AdminRecentStatsTests
{
    private static readonly DateTimeOffset Vor5Tagen = DateTimeOffset.UtcNow.AddDays(-5);
    private static readonly DateTimeOffset Vor40Tagen = DateTimeOffset.UtcNow.AddDays(-40);

    private static (AdminService Dienst, ApplicationDbContext Db, FakeUserLookupService Lookup) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return (new AdminService(db, lookup, new FakeRefreshTokenService(), new AccountDataService(db, lookup)), db, lookup);
    }

    private static Guid NeuesKonto(FakeUserLookupService lookup, DateTimeOffset angelegt)
    {
        var id = Guid.NewGuid();
        lookup.Register(id, $"{id}@test.de", createdAt: angelegt);
        return id;
    }

    private static Dog Hund(ApplicationDbContext db, Guid besitzer, bool geloescht = false)
    {
        var hund = new Dog { Name = "Bello", DeletedAt = geloescht ? DateTimeOffset.UtcNow : null };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        return hund;
    }

    private static async Task<AdminRecentStatsDto> Holen(AdminService dienst) =>
        (await dienst.GetStatsAsync()).Value!.Last30Days;

    [Fact]
    public async Task LeereDatenbank_AllesNull()
    {
        var (dienst, _, _) = Aufsetzen();

        var z = await Holen(dienst);

        Assert.Equal(new AdminRecentStatsDto(0, 0, 0, 0, 0, 0), z);
    }

    [Fact]
    public async Task NeueKonten_ZaehltNurDieLetzten30Tage()
    {
        var (dienst, _, lookup) = Aufsetzen();
        NeuesKonto(lookup, Vor5Tagen);
        NeuesKonto(lookup, Vor5Tagen);
        NeuesKonto(lookup, Vor40Tagen);

        var z = await Holen(dienst);

        Assert.Equal(2, z.NewAccounts);
    }

    [Fact]
    public async Task MitHund_ZaehltKontenNichtHunde_UndIgnoriertGeloeschteHunde()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var zweiHunde = NeuesKonto(lookup, Vor5Tagen);
        var nurGeloescht = NeuesKonto(lookup, Vor5Tagen);
        NeuesKonto(lookup, Vor5Tagen);
        var altesKonto = NeuesKonto(lookup, Vor40Tagen);
        Hund(db, zweiHunde);
        Hund(db, zweiHunde);
        Hund(db, nurGeloescht, geloescht: true);
        Hund(db, altesKonto);
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(3, z.NewAccounts);
        Assert.Equal(1, z.WithDog);
    }

    [Fact]
    public async Task MitHund_EineOffeneEinladungZaehltNicht()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var eingeladen = NeuesKonto(lookup, Vor5Tagen);
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = eingeladen, Status = DogOwnerStatus.Invited });
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        // Solange die Einladung nicht angenommen ist, hat die Person keinen Hund.
        Assert.Equal(0, z.WithDog);
    }

    [Fact]
    public async Task ImVerein_NurGenehmigteMitgliedschaften()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        var genehmigt = NeuesKonto(lookup, Vor5Tagen);
        var abgelehnt = NeuesKonto(lookup, Vor5Tagen);
        var offen = NeuesKonto(lookup, Vor5Tagen);
        var verlassen = NeuesKonto(lookup, Vor5Tagen);
        var alt = NeuesKonto(lookup, Vor40Tagen);
        db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = verein.Id, UserId = genehmigt, Status = ClubMembershipStatus.Approved },
            new ClubMembership { ClubId = verein.Id, UserId = abgelehnt, Status = ClubMembershipStatus.Rejected },
            new ClubMembership { ClubId = verein.Id, UserId = offen, Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = verein.Id, UserId = verlassen, Status = ClubMembershipStatus.Approved, DeletedAt = DateTimeOffset.UtcNow },
            new ClubMembership { ClubId = verein.Id, UserId = alt, Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(1, z.InClub);
    }

    [Fact]
    public async Task ViaClubLink_ZaehltJedenStatusAberNurNeueKonten()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        var genehmigt = NeuesKonto(lookup, Vor5Tagen);
        var abgelehnt = NeuesKonto(lookup, Vor5Tagen);
        var ueberListe = NeuesKonto(lookup, Vor5Tagen);
        var alt = NeuesKonto(lookup, Vor40Tagen);
        db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = verein.Id, UserId = genehmigt, Status = ClubMembershipStatus.Approved, Source = ClubMembershipSource.InviteLink },
            new ClubMembership { ClubId = verein.Id, UserId = abgelehnt, Status = ClubMembershipStatus.Rejected, Source = ClubMembershipSource.InviteLink },
            new ClubMembership { ClubId = verein.Id, UserId = ueberListe, Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = verein.Id, UserId = alt, Status = ClubMembershipStatus.Pending, Source = ClubMembershipSource.InviteLink });
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(2, z.ViaClubLink);
    }

    [Fact]
    public async Task MitZiel_AktivUndErreichtZaehlen_AbgebrochenUndGeloeschterHundNicht()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var aktiv = NeuesKonto(lookup, Vor5Tagen);
        var erreicht = NeuesKonto(lookup, Vor5Tagen);
        var abgebrochen = NeuesKonto(lookup, Vor5Tagen);
        var geloeschterHund = NeuesKonto(lookup, Vor5Tagen);
        var mehrere = NeuesKonto(lookup, Vor5Tagen);
        var alt = NeuesKonto(lookup, Vor40Tagen);

        void Ziel(Dog hund, GoalStatus status) => db.Goals.Add(new Goal
        {
            DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), Status = status,
        });

        Ziel(Hund(db, aktiv), GoalStatus.Active);
        Ziel(Hund(db, erreicht), GoalStatus.Achieved);
        Ziel(Hund(db, abgebrochen), GoalStatus.Cancelled);
        Ziel(Hund(db, geloeschterHund, geloescht: true), GoalStatus.Active);
        var hundMitZweiZielen = Hund(db, mehrere);
        Ziel(hundMitZweiZielen, GoalStatus.Active);
        Ziel(hundMitZweiZielen, GoalStatus.Active);
        Ziel(Hund(db, alt), GoalStatus.Active);
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(3, z.WithGoal);
    }

    [Fact]
    public async Task AktiveKonten_ZaehltTrainingUndFaehrte_EinmalJePerson_AuchAlteKonten()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var nurTraining = NeuesKonto(lookup, Vor40Tagen); // altes Konto, trotzdem aktiv
        var nurFaehrte = NeuesKonto(lookup, Vor5Tagen);
        var beides = NeuesKonto(lookup, Vor5Tagen);
        var nurAlteEintraege = NeuesKonto(lookup, Vor40Tagen);
        var nurGeloescht = NeuesKonto(lookup, Vor40Tagen);
        NeuesKonto(lookup, Vor5Tagen); // neu, aber ohne Eintrag: nicht aktiv

        TrainingSession Training(Guid nutzer, DateTimeOffset angelegt, bool geloescht = false)
        {
            var s = new TrainingSession
            {
                UserId = nutzer, DogId = Guid.NewGuid(), Date = DateOnly.FromDateTime(DateTime.UtcNow),
                DurationMinutes = 30, CreatedAt = angelegt, DeletedAt = geloescht ? DateTimeOffset.UtcNow : null,
            };
            db.TrainingSessions.Add(s);
            return s;
        }

        GpsTrack Faehrte(TrainingSession s, DateTimeOffset angelegt)
        {
            var t = new GpsTrack { TrainingSessionId = s.Id, CreatedAt = angelegt };
            db.GpsTracks.Add(t);
            return t;
        }

        Training(nurTraining, Vor5Tagen);
        // Die Fährte ist neu, das Training dazu alt: zählt für die Person des Trainings.
        Faehrte(Training(nurFaehrte, Vor40Tagen), Vor5Tagen);
        Training(beides, Vor5Tagen);
        Training(beides, Vor5Tagen);
        Faehrte(Training(beides, Vor40Tagen), Vor5Tagen);
        Faehrte(Training(nurAlteEintraege, Vor40Tagen), Vor40Tagen);
        Training(nurGeloescht, Vor5Tagen, geloescht: true);
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(3, z.ActiveAccounts);
    }

    [Fact]
    public async Task NeueKontoZahlenSindTeilmengenDerNeuenKonten()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var konto = NeuesKonto(lookup, Vor5Tagen);
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        var hund = Hund(db, konto);
        db.Goals.Add(new Goal { DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) });
        db.ClubMemberships.Add(new ClubMembership
        {
            ClubId = verein.Id, UserId = konto, Status = ClubMembershipStatus.Approved, Source = ClubMembershipSource.InviteLink,
        });
        await db.SaveChangesAsync();

        var z = await Holen(dienst);

        Assert.Equal(new AdminRecentStatsDto(1, 1, 1, 1, 1, 0), z);
    }
}
