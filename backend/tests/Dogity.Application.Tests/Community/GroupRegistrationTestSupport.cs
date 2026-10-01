using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Gemeinsamer Ausgangszustand der Anmelde-Tests: ein Verein mit Gruppe,
/// Leitung, Co-Trainer:in, Vereins-Trainer:in und einer Fremden.
/// </summary>
public record AnmeldeAufbau(
    ApplicationDbContext Db,
    GroupRegistrationService Dienst,
    GroupRegistrationFormService Formular,
    FakeNotificationService Meldungen,
    Club Verein,
    Group Gruppe,
    Guid Leitung,
    Guid CoTrainerin,
    Guid VereinsTrainerin,
    Guid Fremde)
{
    public static async Task<AnmeldeAufbau> ErzeugeAsync(bool mitCode = false)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var verein = new Club { Name = "Hundesportverein Nord" };
        var leitung = Guid.NewGuid();
        var co = Guid.NewGuid();
        var vereinsTrainerin = Guid.NewGuid();
        var gruppe = new Group
        {
            ClubId = verein.Id,
            TrainerId = leitung,
            Name = "Welpengruppe",
            RegistrationCode = mitCode ? ClubInviteCode.Erzeugen() : null,
        };
        db.Clubs.Add(verein);
        db.Groups.Add(gruppe);
        db.GroupTrainers.Add(new GroupTrainer { GroupId = gruppe.Id, UserId = co, Status = GroupTrainerStatus.Active });
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = vereinsTrainerin, Role = ClubRole.Training });
        await db.SaveChangesAsync();
        return new AnmeldeAufbau(
            db, new GroupRegistrationService(db), new GroupRegistrationFormService(db, meldungen), meldungen,
            verein, gruppe, leitung, co, vereinsTrainerin, Guid.NewGuid());
    }

    /// <summary>Eine gültige manuelle Eingabe; einzelne Felder lassen sich überschreiben.</summary>
    public static GroupRegistrationRequest Eingabe(
        string vorname = "Anna", string nachname = "Muster", string rufname = "Bella", string rasse = "Labrador",
        DateOnly? wurftag = null, string telefon = "0721 123456", string? notiz = null) =>
        new(vorname, nachname, rufname, rasse, wurftag ?? Vereinszeit.Heute().AddMonths(-3), telefon, notiz);

    /// <summary>Eine gültige Formular-Eingabe mit Einwilligung.</summary>
    public static PublicRegistrationRequest Formulareingabe(
        string vorname = "Anna", string nachname = "Muster", string rufname = "Bella", string rasse = "Labrador",
        DateOnly? wurftag = null, string telefon = "0721 123456", bool einwilligung = true, string? koeder = null) =>
        new(vorname, nachname, rufname, rasse, wurftag ?? Vereinszeit.Heute().AddMonths(-3), telefon, einwilligung, koeder);

    public async Task<GroupRegistration> AnmeldungAsync(
        string rufname = "Bella", string telefon = "0721 123456", DateTimeOffset? angemeldet = null)
    {
        var zeile = new GroupRegistration
        {
            GroupId = Gruppe.Id,
            FirstName = "Anna", LastName = "Muster", DogName = rufname, DogBreed = "Labrador",
            DogBirthDate = Vereinszeit.Heute().AddMonths(-3), Phone = telefon,
            RegisteredAt = angemeldet ?? DateTimeOffset.UtcNow,
        };
        Db.GroupRegistrations.Add(zeile);
        await Db.SaveChangesAsync();
        return zeile;
    }
}
