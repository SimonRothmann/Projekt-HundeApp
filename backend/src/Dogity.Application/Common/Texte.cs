namespace Dogity.Application.Common;

/// <summary>Kleine Helfer für Freitexte aus Anfragen, die mehrere Dienste brauchen.</summary>
internal static class Texte
{
    /// <summary>Getrimmter Text oder null, wenn er leer ist - optionale Felder speichern "nichts" als null, nie als "".</summary>
    public static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
