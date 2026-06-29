namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Rollen aus DATABASE.md "roles" - ein Benutzer kann mehrere davon
/// gleichzeitig besitzen (siehe MASTER_PROMPT.md "Rollenmodell").
/// </summary>
public static class Roles
{
    public const string User = "USER";
    public const string Trainer = "TRAINER";
    public const string ClubAdmin = "CLUB_ADMIN";
    public const string Judge = "JUDGE";
    public const string Admin = "ADMIN";

    public static readonly string[] All = [User, Trainer, ClubAdmin, Judge, Admin];
}
