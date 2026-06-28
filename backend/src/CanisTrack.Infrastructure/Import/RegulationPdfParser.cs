using System.Diagnostics;
using System.Text.RegularExpressions;
using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using Microsoft.Extensions.Configuration;

namespace CanisTrack.Infrastructure.Import;

/// <summary>
/// Extrahiert Übungsname+Punktzahl-Kandidaten aus der lokalen
/// Prüfungsordnungs-PDF über das externe, freie Tool "pdftotext"
/// (Teil von poppler-utils - bewusst kein NuGet-Paket für PDF-Parsing,
/// siehe TODO.md "Offene Entscheidungen": ein naheliegendes Paket erwies
/// sich beim Prüfen der Versionshistorie als verdächtig/vermutlich
/// kompromittiert und wurde verworfen).
/// </summary>
public class RegulationPdfParser(IConfiguration configuration) : IRegulationPdfParser
{
    private static readonly Regex CandidatePattern = new(@"(?<name>\S.*?)\s+(?<points>\d{1,3})\s+Punkte\b", RegexOptions.Compiled);

    private static readonly string[] ExcludeKeywords =
    [
        "Abzug", "Entwertung", "minus", "bis zu", "Bewertung 0", "Kommt", "•"
    ];

    public async Task<Result<IReadOnlyList<ParsedExerciseCandidate>>> ScanAsync(CancellationToken ct = default)
    {
        var pdfPath = configuration["RegulationImport:PdfPath"];
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            return Result<IReadOnlyList<ParsedExerciseCandidate>>.Failure(
                "Keine lokale Prüfungsordnungs-PDF gefunden. Pfad unter 'RegulationImport:PdfPath' konfigurieren.");
        }

        string text;
        try
        {
            text = await RunPdfToTextAsync(pdfPath, ct);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Result<IReadOnlyList<ParsedExerciseCandidate>>.Failure(
                $"pdftotext konnte nicht ausgeführt werden ({ex.Message}). Ist poppler-utils installiert?");
        }

        return Result<IReadOnlyList<ParsedExerciseCandidate>>.Success(ExtractCandidates(text));
    }

    private async Task<string> RunPdfToTextAsync(string pdfPath, CancellationToken ct)
    {
        var pdftotextPath = configuration["RegulationImport:PdftotextPath"] ?? "pdftotext";

        var psi = new ProcessStartInfo
        {
            FileName = pdftotextPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        psi.ArgumentList.Add("-layout");
        psi.ArgumentList.Add("-enc");
        psi.ArgumentList.Add("UTF-8");
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add("-");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(await stderrTask);

        return await stdoutTask;
    }

    private static IReadOnlyList<ParsedExerciseCandidate> ExtractCandidates(string text)
    {
        var results = new List<ParsedExerciseCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || ExcludeKeywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Mehrspaltige Tabellenzeilen (mehrere Prüfungsstufen nebeneinander)
            // ergeben mehrere Treffer pro Zeile und werden als nicht eindeutig
            // zuordenbar übersprungen - lieber kein Vorschlag als ein falscher.
            var matches = CandidatePattern.Matches(line);
            if (matches.Count != 1)
                continue;

            var name = matches[0].Groups["name"].Value.Trim(' ', '.', '-');
            if (name.Length < 3 || name.Length > 80 || !char.IsUpper(name[0]))
                continue;

            var points = int.Parse(matches[0].Groups["points"].Value);
            if (points is < 1 or > 100)
                continue;

            if (seen.Add($"{name.ToLowerInvariant()}|{points}"))
                results.Add(new ParsedExerciseCandidate(name, points));
        }

        return results;
    }
}
