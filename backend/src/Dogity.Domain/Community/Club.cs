using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Ein Verein (siehe DATABASE.md "Multi Tenant Struktur" - ein Verein ist
/// langfristig ein Tenant). MVP-Funktionsumfang: einfache Stammdaten,
/// Mitgliederverwaltung folgt über <see cref="Group"/>.
/// </summary>
public class Club : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<ClubTrainer> Trainers { get; set; } = new List<ClubTrainer>();
    public ICollection<ClubMembership> Memberships { get; set; } = new List<ClubMembership>();
}
