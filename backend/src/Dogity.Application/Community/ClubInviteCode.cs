using System.Security.Cryptography;

namespace Dogity.Application.Community;

/// <summary>
/// Der geheime Teil eines Vereins-Einladungslinks.
///
/// 128 Bit aus dem Zufallsgenerator des Betriebssystems, URL-sicher kodiert
/// (Base64 mit - und _ statt + und /, ohne Füllzeichen): 22 Zeichen, die sich
/// ohne Maskierung in Link und QR-Code setzen lassen. Erraten ist damit keine
/// Option; wer den Code nicht kennt, kommt nicht an den Vereinsnamen.
/// </summary>
public static class ClubInviteCode
{
    /// <summary>Länge eines erzeugten Codes (16 Byte in Base64Url ohne Padding).</summary>
    public const int Laenge = 22;

    public static string Erzeugen()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Ob <paramref name="code"/> die Form eines erzeugten Codes hat. Wird VOR
    /// der Datenbankabfrage geprüft: Was nicht so aussehen kann, braucht keine
    /// Abfrage und wird genauso beantwortet wie ein abgeschalteter Code.
    /// </summary>
    public static bool IstGueltigeForm(string? code)
    {
        if (code is null || code.Length != Laenge) return false;
        foreach (var c in code)
        {
            var erlaubt = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
            if (!erlaubt) return false;
        }
        return true;
    }
}
