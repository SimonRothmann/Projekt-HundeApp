using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Dogity.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Wer ohne Anmeldung an einen Endpunkt kommt, steht als Attribut am Controller,
/// nicht in einem Service - und es gibt keine Fallback-Policy: Controller, die
/// direkt von ControllerBase erben (Auth, Sports, System), sind ohne
/// [Authorize] anonym. Eine neue Aktion dort ohne Attribut wäre still öffentlich,
/// ein entferntes [EnableRateLimiting] oder ein Tippfehler im Policy-Namen fiele
/// erst im Betrieb auf. Dieser Wächter hält beides fest.
/// </summary>
public class AnonymeEndpunkteTests
{
    /// <summary>
    /// Jede anonyme Aktion mit ihrer Drosselung (null = bewusst ohne: reine
    /// Katalog-Lesezugriffe ohne personenbezogene Daten und ohne Kosten).
    /// Kommt eine anonyme Aktion dazu, gehört sie hier mit Begründung hinein.
    /// </summary>
    private static readonly Dictionary<string, string?> Erwartet = new()
    {
        ["AuthController.Register"] = "auth",
        ["AuthController.Login"] = "auth",
        ["AuthController.ForgotPassword"] = "auth",
        ["AuthController.ResetPassword"] = "auth",
        ["AuthController.Refresh"] = "refresh",
        ["AuthController.Logout"] = "refresh",
        ["ClubsController.GetInvitePreview"] = "invite",
        ["GroupRegistrationsController.GetForm"] = "invite",
        ["GroupRegistrationsController.Submit"] = "anmeldung",
        ["SachkundeController.StartExam"] = "pruefung",
        ["SachkundeController.SubmitExam"] = "pruefung",
        ["EmailAbmeldungController.Setze"] = "abmeldung",
        ["SachkundeController.GetCatalogs"] = null,
        ["SachkundeController.GetQuestions"] = null,
        ["SportsController.GetSports"] = null,
        ["SportsController.GetCountries"] = null,
        ["SportsController.GetRegulations"] = null,
        ["SportsController.GetRegulationDetail"] = null,
        ["SystemController.Features"] = null,
    };

    private sealed record Aktion(string Name, bool Anonym, string? Drosselung);

    private static List<Aktion> AlleAktionen() =>
        typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m => new Aktion(
                    $"{t.Name}.{m.Name}",
                    IstAnonym(t, m),
                    (m.GetCustomAttribute<EnableRateLimitingAttribute>()
                        ?? t.GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true))?.PolicyName)))
            .ToList();

    private static bool IstAnonym(Type controller, MethodInfo methode)
    {
        if (methode.GetCustomAttribute<AllowAnonymousAttribute>() is not null) return true;
        if (controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null) return true;
        // Mehrere [Authorize] an einer Stelle sind erlaubt (Rolle + Basisklasse).
        return !methode.GetCustomAttributes<AuthorizeAttribute>().Any()
            && !controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }

    [Fact]
    public void Waechter_FindetAktionen()
    {
        // Sonst wäre jeder folgende Test grün, weil er nichts ansieht.
        Assert.True(AlleAktionen().Count > 100);
    }

    [Fact]
    public void GenauDieErwartetenAktionenSindAnonym()
    {
        var anonym = AlleAktionen().Where(a => a.Anonym).Select(a => a.Name).Order().ToList();

        Assert.Equal(Erwartet.Keys.Order().ToList(), anonym);
    }

    [Fact]
    public void AnonymeAktionen_HabenIhreDrosselung()
    {
        var abweichend = AlleAktionen()
            .Where(a => a.Anonym && Erwartet.TryGetValue(a.Name, out var policy) && policy != a.Drosselung)
            .Select(a => $"{a.Name}: {a.Drosselung ?? "ohne"} statt {Erwartet[a.Name] ?? "ohne"}");

        Assert.Empty(abweichend);
    }

    [Fact]
    public void JedeVerwendeteDrosselung_IstInProgramCsDefiniert()
    {
        var definiert = Regex.Matches(File.ReadAllText(ProgramCs()), @"AddPolicy\(""(\w+)"",\s*httpContext")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
        var verwendet = AlleAktionen().Select(a => a.Drosselung).OfType<string>().ToHashSet();

        Assert.NotEmpty(definiert);
        Assert.Subset(definiert, verwendet);
        // Umgekehrt: eine Policy, die niemand nutzt, ist entweder toter Code oder ein vergessenes Attribut.
        Assert.Equal(definiert.Order(), verwendet.Order());
    }

    private static string ProgramCs([CallerFilePath] string datei = "") =>
        // .../backend/tests/Dogity.Application.Tests/Identity/AnonymeEndpunkteTests.cs -> .../backend/src/Dogity.Api/Program.cs
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(datei)!, "..", "..", "..", "src", "Dogity.Api", "Program.cs"));
}
