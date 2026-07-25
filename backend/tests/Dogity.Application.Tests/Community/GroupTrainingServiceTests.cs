using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Testet GroupTrainingService (siehe docs/GROUP_TRAINING_PLANS.md): Trainer-
/// Gating, Bibliothek (System-Vorlagen + eigene), Erstellen/Bearbeiten/Löschen
/// eigener Einheiten sowie das Kopieren einer Vorlage in eine eigene Gruppe.
/// System-Vorlagen (CreatedByUserId == null) dürfen nie verändert werden.
/// </summary>
public class GroupTrainingServiceTests
{
    private static GroupTrainingService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GroupTrainingService(db);
    }

    /// <summary>Macht den Nutzer datengetrieben zum Trainer (leitet eine Gruppe) und gibt die GroupId zurück.</summary>
    private static async Task<Guid> MakeTrainerWithGroupAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db, Guid trainerId)
    {
        var group = new Group { TrainerId = trainerId, Name = "Gruppe" };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    private static async Task<GroupTrainingUnit> AddTemplateAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, GroupTrainingCategory category, string title, int items = 2)
    {
        var unit = new GroupTrainingUnit { Title = title, Category = category, CreatedByUserId = null, GroupId = null };
        for (var i = 0; i < items; i++)
            unit.Items.Add(new GroupTrainingUnitItem { Title = $"Übung {i + 1}", Focus = "Sozialisierung", DurationMinutes = 10, SortOrder = i });
        db.GroupTrainingUnits.Add(unit);
        await db.SaveChangesAsync();
        return unit;
    }

    [Fact]
    public async Task GetLibrary_NonTrainer_Fails()
    {
        var service = MakeService(out var db);
        await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");

        var result = await service.GetLibraryAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetLibrary_Trainer_ReturnsTemplatesAndOwnUnits()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");
        await AddTemplateAsync(db, GroupTrainingCategory.YoungDog, "Junghunde 1");
        await service.CreateUnitAsync(trainerId, new CreateGroupTrainingUnitRequest(
            "Meine Einheit", null, GroupTrainingCategory.General, null,
            [new GroupTrainingItemInput("Aufwärmen")]));

        var result = await service.GetLibraryAsync(trainerId);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Templates.Count);
        Assert.All(result.Value.Templates, t => Assert.True(t.IsTemplate));
        Assert.Single(result.Value.Mine);
        Assert.True(result.Value.Mine[0].IsMine);
        Assert.False(result.Value.Mine[0].IsTemplate);
    }

    [Fact]
    public async Task CreateUnit_Trainer_PersistsWithItemsAndTotalMinutes()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);

        var result = await service.CreateUnitAsync(trainerId, new CreateGroupTrainingUnitRequest(
            "Welpenstunde", "Beschreibung", GroupTrainingCategory.Puppy, null,
            [
                new GroupTrainingItemInput("Sozialkontakt", "…", "Sozialisierung", 10),
                new GroupTrainingItemInput("Ruheübung", "…", "Entspannung", 5),
            ]));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(15, result.Value.TotalMinutes);
        // Leere Titel werden übersprungen.
        Assert.Equal("Sozialkontakt", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task CreateUnit_ForGroupNotLed_Fails()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        // Fremde Gruppe eines anderen Trainers.
        var foreignGroupId = await MakeTrainerWithGroupAsync(db, Guid.NewGuid());

        var result = await service.CreateUnitAsync(trainerId, new CreateGroupTrainingUnitRequest(
            "Fremd", null, GroupTrainingCategory.General, foreignGroupId,
            [new GroupTrainingItemInput("X")]));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateUnit_ReplacesItems()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        var created = await service.CreateUnitAsync(trainerId, new CreateGroupTrainingUnitRequest(
            "Titel", null, GroupTrainingCategory.General, null,
            [new GroupTrainingItemInput("A"), new GroupTrainingItemInput("B")]));

        var result = await service.UpdateUnitAsync(trainerId, created.Value!.Id, new UpdateGroupTrainingUnitRequest(
            "Neuer Titel", "neu", GroupTrainingCategory.Puppy,
            [new GroupTrainingItemInput("C", null, "Rückruf", 8)]));

        Assert.True(result.Succeeded);
        Assert.Equal("Neuer Titel", result.Value!.Title);
        Assert.Equal(GroupTrainingCategory.Puppy, result.Value.Category);
        Assert.Single(result.Value.Items);
        Assert.Equal("C", result.Value.Items[0].Title);
        // Alte Items sind wirklich entfernt (auch ohne QueryFilter).
        var remaining = await db.GroupTrainingUnitItems.IgnoreQueryFilters()
            .Where(i => i.GroupTrainingUnitId == created.Value.Id && i.DeletedAt == null)
            .CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task UpdateUnit_SystemTemplate_Fails()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        var template = await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");

        var result = await service.UpdateUnitAsync(trainerId, template.Id, new UpdateGroupTrainingUnitRequest(
            "Gehackt", null, GroupTrainingCategory.Puppy, [new GroupTrainingItemInput("X")]));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteUnit_Own_SoftDeletesAndDropsFromLibrary()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        var created = await service.CreateUnitAsync(trainerId, new CreateGroupTrainingUnitRequest(
            "Weg damit", null, GroupTrainingCategory.General, null, [new GroupTrainingItemInput("A")]));

        var del = await service.DeleteUnitAsync(trainerId, created.Value!.Id);

        Assert.True(del.Succeeded);
        var lib = await service.GetLibraryAsync(trainerId);
        Assert.Empty(lib.Value!.Mine);
        var row = await db.GroupTrainingUnits.IgnoreQueryFilters().SingleAsync(u => u.Id == created.Value.Id);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task DeleteUnit_SystemTemplate_Fails()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        var template = await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");

        var result = await service.DeleteUnitAsync(trainerId, template.Id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CopyUnitToGroup_FromTemplate_CreatesEditableGroupCopy()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        var groupId = await MakeTrainerWithGroupAsync(db, trainerId);
        var template = await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1", items: 3);

        var result = await service.CopyUnitToGroupAsync(trainerId, template.Id, groupId);

        Assert.True(result.Succeeded);
        Assert.Equal(groupId, result.Value!.GroupId);
        Assert.True(result.Value.IsMine);
        Assert.False(result.Value.IsTemplate);
        Assert.Equal(3, result.Value.Items.Count);
        // Die kopierte Einheit ist jetzt bearbeitbar.
        var edit = await service.UpdateUnitAsync(trainerId, result.Value.Id, new UpdateGroupTrainingUnitRequest(
            "Angepasst", null, GroupTrainingCategory.Puppy, [new GroupTrainingItemInput("Nur eins")]));
        Assert.True(edit.Succeeded);
    }

    [Fact]
    public async Task CopyUnitToGroup_NotGroupTrainer_Fails()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        await MakeTrainerWithGroupAsync(db, trainerId);
        var foreignGroupId = await MakeTrainerWithGroupAsync(db, Guid.NewGuid());
        var template = await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");

        var result = await service.CopyUnitToGroupAsync(trainerId, template.Id, foreignGroupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetGroupUnits_OwningTrainer_ReturnsOnlyThatGroupsUnits()
    {
        var service = MakeService(out var db);
        var trainerId = Guid.NewGuid();
        var groupId = await MakeTrainerWithGroupAsync(db, trainerId);
        var template = await AddTemplateAsync(db, GroupTrainingCategory.Puppy, "Welpen 1");
        await service.CopyUnitToGroupAsync(trainerId, template.Id, groupId);

        var result = await service.GetGroupUnitsAsync(trainerId, groupId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
        Assert.Equal(groupId, result.Value![0].GroupId);
    }
}
