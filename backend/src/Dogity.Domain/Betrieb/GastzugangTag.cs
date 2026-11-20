namespace Dogity.Domain.Betrieb;

/// <summary>
/// Zählt die Gastzugänge ("App ausprobieren", docs/GASTZUGANG.md) je Tag in Vereinszeit:
/// wie viele angelegt wurden und wie viele Gäste sich danach über das Banner registriert haben.
/// Reine Zählung ohne Personenbezug - deshalb kein Soft Delete, keine Id und kein Löschjob;
/// die Zeilen bleiben stehen, damit die Statistik 7 und 30 Tage zurückblicken kann.
/// </summary>
public class GastzugangTag
{
    public DateOnly Day { get; set; }

    /// <summary>Angelegte Gastzugänge an diesem Tag.</summary>
    public int Created { get; set; }

    /// <summary>Registrierungen, die aus dem Gast-Banner kamen.</summary>
    public int Registered { get; set; }
}
