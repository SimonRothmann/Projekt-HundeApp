using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Eine frische, echte Postgres-Datenbank für die Tests unter <c>Postgres/</c>.
///
/// Alle anderen Tests laufen gegen EF In-Memory - dort wird nie SQL erzeugt, geschweige
/// denn ausgeführt. Eine Abfrage, die in SQL Zeilen vervielfacht oder gar nicht übersetzt
/// werden kann, fällt damit erst auf dem Server auf. Diese Gruppe wendet die Migrationen
/// auf eine echte Datenbank an und führt die wichtigsten Abfragewege einmal wirklich aus.
///
/// Die Verbindung kommt aus der Umgebungsvariable <see cref="Variable"/> (Server mit dem
/// Recht, Datenbanken anzulegen, z. B. <c>Host=localhost;Username=postgres;Password=…</c>).
/// Ist sie nicht gesetzt, werden die Tests übersprungen - so läuft <c>dotnet test</c>
/// lokal ohne Postgres durch. In der CI (<c>CI=true</c>) gilt das Überspringen nicht:
/// ein vergessener Eintrag im Workflow lässt die Tests scheitern, statt sie still
/// auszulassen.
/// </summary>
public sealed class PostgresDatenbank : IAsyncLifetime
{
    public const string Variable = "DOGITY_TEST_POSTGRES";

    internal static string? Verbindung => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } v ? v : null;

    internal static bool Ueberspringen =>
        Verbindung is null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    private string? _datenbank;
    private string? _verbindung;

    public async Task InitializeAsync()
    {
        if (Ueberspringen) return;
        var server = Verbindung ?? throw new InvalidOperationException(
            $"In der CI muss {Variable} gesetzt sein (Verbindung zu einem Postgres mit Recht, Datenbanken anzulegen).");

        _datenbank = "dogity_pgtest_" + Guid.NewGuid().ToString("N");
        await using (var verbindung = new NpgsqlConnection(server))
        {
            await verbindung.OpenAsync();
            await using var befehl = new NpgsqlCommand($"CREATE DATABASE \"{_datenbank}\"", verbindung);
            await befehl.ExecuteNonQueryAsync();
        }

        _verbindung = new NpgsqlConnectionStringBuilder(server) { Database = _datenbank }.ConnectionString;

        // Wie beim Start des Backends (Program.cs): alle Migrationen, von leer bis heute.
        await using var db = NeuerKontext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_datenbank is null) return;
        NpgsqlConnection.ClearAllPools();
        await using var verbindung = new NpgsqlConnection(Verbindung);
        await verbindung.OpenAsync();
        await using var befehl = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_datenbank}\" WITH (FORCE)", verbindung);
        await befehl.ExecuteNonQueryAsync();
    }

    /// <summary>Ein neuer Kontext auf die Testdatenbank (je Test einer, wie im Backend je Anfrage).</summary>
    public ApplicationDbContext NeuerKontext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_verbindung!).Options);
}

/// <summary>Ein Test, der Postgres braucht: wird übersprungen, solange <c>DOGITY_TEST_POSTGRES</c> fehlt (außer in der CI).</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresDatenbank.Ueberspringen)
            Skip = $"Kein Postgres: {PostgresDatenbank.Variable} nicht gesetzt.";
    }
}
