namespace Dogity.Application.Common;

/// <summary>SQLSTATE-Codes von Postgres, auf die Dienste gezielt reagieren (statt einen Fehler durchzureichen).</summary>
internal static class SqlStates
{
    /// <summary>"unique_violation": ein eindeutiger Index wurde verletzt, etwa bei zwei gleichzeitigen Anfragen.</summary>
    public const string EindeutigerIndexVerletzt = "23505";
}
