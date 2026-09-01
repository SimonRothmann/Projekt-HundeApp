namespace Dogity.Application.Common;

/// <summary>
/// Strukturiertes Ergebnis für Use Cases (siehe CODING_GUIDELINES.md
/// "Fehlerbehandlung: Nie Exception anzeigen, immer strukturierte Fehler").
/// Controller mappen <see cref="Errors"/> auf passende HTTP-Statuscodes.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Unterscheidet "gibt es nicht (oder darfst du nicht sehen)" von "deine
    /// Eingabe stimmt nicht". Ohne diese Unterscheidung beantwortete die API
    /// JEDEN Fehlschlag eines Result&lt;T&gt; mit 404 - auch "Bewertung muss
    /// zwischen 1 und 5 liegen".
    ///
    /// Bewusst auch für verdeckte Berechtigungsfehler: Wer keinen Zugriff auf
    /// einen Verein hat, bekommt "Verein nicht gefunden" und damit 404 - dass
    /// es ihn gibt, geht ihn nichts an.
    /// </summary>
    public bool IsNotFound { get; }

    protected Result(bool succeeded, IReadOnlyList<string> errors, bool isNotFound = false)
    {
        Succeeded = succeeded;
        Errors = errors;
        IsNotFound = isNotFound;
    }

    public static Result Success() => new(true, []);

    /// <summary>Fachlicher Fehlschlag - der Aufrufer hat etwas falsch gemacht (HTTP 400).</summary>
    public static Result Failure(params string[] errors) => new(false, errors);

    /// <summary>Angefragtes Objekt existiert nicht oder ist für den Aufrufer unsichtbar (HTTP 404).</summary>
    public static Result NotFound(params string[] errors) => new(false, errors, isNotFound: true);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, IReadOnlyList<string> errors, bool isNotFound = false)
        : base(succeeded, errors, isNotFound)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, []);
    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);
    public static new Result<T> NotFound(params string[] errors) => new(false, default, errors, isNotFound: true);
}
