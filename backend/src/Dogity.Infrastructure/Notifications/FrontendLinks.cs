using Dogity.Application.Notifications;
using Microsoft.Extensions.Configuration;

namespace Dogity.Infrastructure.Notifications;

/// <inheritdoc />
public class FrontendLinks(IConfiguration configuration) : IFrontendLinks
{
    private string Basis => configuration["Frontend:BaseUrl"]?.Trim().TrimEnd('/') ?? string.Empty;

    private string ApiBasis => configuration["Api:BaseUrl"]?.Trim().TrimEnd('/') ?? string.Empty;

    public bool HatBasis => Basis.Length > 0;

    public bool HatApiBasis => ApiBasis.Length > 0;

    public string? ApiAbsolut(string pfad) =>
        HatApiBasis && !string.IsNullOrEmpty(pfad) && pfad[0] == '/' && !pfad.StartsWith("//", StringComparison.Ordinal)
            ? ApiBasis + pfad
            : null;

    public string Absolut(string? pfad)
    {
        // Nur Pfade der App: genau ein führender "/". Alles andere (eine fremde
        // Adresse, "//host") bliebe nicht auf unserer Seite.
        var sicher = !string.IsNullOrEmpty(pfad) && pfad[0] == '/' && !pfad.StartsWith("//", StringComparison.Ordinal)
            ? pfad
            : "/dashboard";
        return Basis + sicher;
    }
}
