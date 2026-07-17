namespace Dogity.Api.Contracts;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string RefreshToken, Guid UserId, string Email, string FirstName, string LastName, string[] Roles);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record RefreshTokenRequest(string RefreshToken);
