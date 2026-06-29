using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

public enum DogGender
{
    Male,
    Female
}

/// <summary>
/// Ein Hund. Besitzer werden über <see cref="DogOwner"/> verknüpft,
/// damit mehrere Benutzer (Besitzer, Trainer) denselben Hund verwalten können.
/// </summary>
public class Dog : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateOnly? Birthday { get; set; }
    public DogGender Gender { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }

    public ICollection<DogOwner> Owners { get; set; } = new List<DogOwner>();
}
