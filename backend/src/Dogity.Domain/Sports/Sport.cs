using Dogity.Domain.Common;

namespace Dogity.Domain.Sports;

/// <summary>
/// Eine Hundesportart (z.B. BH, IBGH, Fährte). Sportarten werden bewusst
/// als Daten modelliert, siehe DATABASE.md "Keine Sportart wird hartcodiert" -
/// eine neue Sparte erfordert ausschließlich neue Datensätze, keinen Code.
/// </summary>
public class Sport : Entity
{
    public string Code { get; set; } = string.Empty; // z.B. "BH", "IBGH", "FAERTE"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    public ICollection<Regulation> Regulations { get; set; } = new List<Regulation>();
}
