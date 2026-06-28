namespace CanisTrack.Application.Abstractions;

/// <summary>
/// Liefert den aktuell authentifizierten Benutzer aus dem HTTP-Kontext.
/// Implementierung lebt in CanisTrack.Api, damit Application nicht von
/// ASP.NET Core HttpContext abhängt.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}
