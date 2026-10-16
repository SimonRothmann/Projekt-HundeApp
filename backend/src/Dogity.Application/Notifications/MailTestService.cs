using System.Net.Mail;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class MailTestService(
    IEmailSender sender,
    INotificationChannels channels,
    IFrontendLinks links,
    MailDrossel drossel,
    TimeProvider time) : IMailTestService
{
    /// <summary>
    /// Länger wartet niemand vor einem Knopf. MailKit selbst gäbe erst nach
    /// zwei Minuten auf.
    /// </summary>
    public static readonly TimeSpan Zeitlimit = TimeSpan.FromSeconds(30);

    // Die Antwort eines Mailservers kann lang sein; für die Anzeige reicht der Anfang.
    private const int FehlerHoechstens = 300;

    public async Task<Result<TestmailErgebnisDto>> SendeAsync(string an, CancellationToken ct = default)
    {
        var adresse = an?.Trim() ?? "";
        if (adresse.Length is 0 or > 254
            || adresse.Any(char.IsControl)
            || !MailAddress.TryCreate(adresse, out var geprueft)
            || geprueft.Address != adresse)
            return Result<TestmailErgebnisDto>.Failure("Bitte eine gültige E-Mail-Adresse eingeben.");

        if (!channels.EmailAktiv)
            return Result<TestmailErgebnisDto>.Success(new TestmailErgebnisDto(VersandEingerichtet: false, Versendet: false, Fehler: null));

        if (!drossel.Erlaubt(KontomailArt.Testmail, adresse))
            return Result<TestmailErgebnisDto>.Failure("Für heute sind genug Testmails verschickt. Morgen geht es wieder.");

        var inhalt = MailVorlagen.Testmail(links.Absolut("/dashboard"), time.GetUtcNow());

        using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        zeitlimit.CancelAfter(Zeitlimit);
        try
        {
            await sender.SendAsync(adresse, inhalt.Betreff, inhalt.Text, inhalt.Html, ct: zeitlimit.Token);
            return Result<TestmailErgebnisDto>.Success(new TestmailErgebnisDto(VersandEingerichtet: true, Versendet: true, Fehler: null));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<TestmailErgebnisDto>.Success(new TestmailErgebnisDto(true, false,
                $"Der Mailserver hat nicht innerhalb von {Zeitlimit.TotalSeconds:0} Sekunden geantwortet."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Die Meldung geht nur an den Admin zurück, der den Server betreibt -
            // genau sie braucht er, etwa "Sender address rejected".
            var meldung = ex.Message.Length > FehlerHoechstens ? ex.Message[..FehlerHoechstens] + "…" : ex.Message;
            return Result<TestmailErgebnisDto>.Success(new TestmailErgebnisDto(true, false, meldung));
        }
    }
}
