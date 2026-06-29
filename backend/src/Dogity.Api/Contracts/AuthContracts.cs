namespace Dogity.Api.Contracts;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid UserId, string Email, string FirstName, string LastName, string[] Roles);
