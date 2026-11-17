using System.Diagnostics;
using Dogity.Application.Abstractions;
using Dogity.Application.Admin;
using Dogity.Application.Dashboard;
using Dogity.Application.Dogs;
using Dogity.Application.Notifications;
using Dogity.Application.Planning;
using Dogity.Application.Stats;
using Dogity.Application.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Api.Hosting;

/// <summary>
/// Führt nach dem Start einmal die Abfragen der meistbesuchten Seiten aus
/// (Startseite, Hundeseite, Statistik, Admin), damit nicht die erste Nutzer:in
/// nach einem Deploy das Übersetzen der Datenbankabfragen und des Codes abwartet.
/// Gemessen auf Test: Die Startseite brauchte beim ersten Aufruf 1,2 s statt
/// 0,2-0,4 s, kleine Abfragen 140-500 ms statt 2-65 ms.
///
/// Nur lesende Dienstaufrufe. Die Daten nimmt er von der Person, die zuletzt ein
/// Training eingetragen hat, weil nur echte Daten alle Zweige einer Abfrage
/// durchlaufen. Die Ergebnisse verwirft er, nichts wird ausgegeben. Ein Fehler
/// wird nur protokolliert, der Start hängt nicht davon ab.
/// </summary>
public class WarmstartBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<WarmstartBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var uhr = Stopwatch.StartNew();
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IApplicationDbContext>();

        Guid nutzer = Guid.Empty, hund = Guid.Empty;
        try
        {
            var training = await db.TrainingSessions.AsNoTracking()
                .OrderByDescending(t => t.Date)
                .Select(t => new { t.UserId, t.DogId })
                .FirstOrDefaultAsync(ct);
            (nutzer, hund) = (training?.UserId ?? Guid.Empty, training?.DogId ?? Guid.Empty);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Ein unbehandelter Fehler in einem Hintergrunddienst hielte die ganze App an.
            logger.LogWarning(ex, "Warmstart: Auswahl der Daten fehlgeschlagen.");
            return;
        }

        (string Name, Func<Task> Abfrage)[] schritte =
        [
            ("Startseite", () => sp.GetRequiredService<IDashboardService>().GetAsync(nutzer, ct)),
            ("Hund", () => sp.GetRequiredService<IDogService>().GetByIdAsync(nutzer, hund, ct)),
            ("Ziele", () => sp.GetRequiredService<IGoalService>().GetByDogAsync(nutzer, hund, ct: ct)),
            ("Trainings", () => sp.GetRequiredService<ITrainingService>().GetByDogAsync(nutzer, hund, ct: ct)),
            ("Gesundheit", () => sp.GetRequiredService<IDogHealthService>().GetAsync(nutzer, hund, ct)),
            ("Pausen", () => sp.GetRequiredService<IDogPauseService>().GetAsync(nutzer, hund, ct)),
            ("Merkzettel", () => sp.GetRequiredService<IMerkzettelService>().GetAsync(nutzer, hund, ct)),
            ("Benachrichtigungen", () => sp.GetRequiredService<INotificationService>().GetMyNotificationsAsync(nutzer, ct)),
            ("Statistik", () => sp.GetRequiredService<IStatsService>().GetDashboardAsync(nutzer, ct)),
            ("Statistik Übungen", () => sp.GetRequiredService<IStatsService>().GetDogExerciseStatsAsync(nutzer, hund, ct)),
            ("Statistik Fährten", () => sp.GetRequiredService<IStatsService>().GetDogTrackStatsAsync(nutzer, hund, ct)),
            ("Statistik Verfassung", () => sp.GetRequiredService<IStatsService>().GetDogConditionStatsAsync(nutzer, hund, ct)),
            ("Admin", () => sp.GetRequiredService<IAdminService>().GetStatsAsync(ct)),
            ("Admin Nutzung", () => sp.GetRequiredService<IAdminUsageService>().GetAsync(ct)),
        ];

        foreach (var (name, abfrage) in schritte)
        {
            try
            {
                await abfrage();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return; // App fährt herunter.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Warmstart: {Schritt} fehlgeschlagen.", name);
            }
        }

        logger.LogInformation("Warmstart: {Anzahl} Abfragen in {Ms} ms vorbereitet.", schritte.Length, uhr.ElapsedMilliseconds);
    }
}
