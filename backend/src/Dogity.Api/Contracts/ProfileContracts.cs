namespace Dogity.Api.Contracts;

public record ProfileDto(string FirstName, string LastName, string Email, string? AvatarUrl);

public record UpdateProfileRequest(string FirstName, string LastName, string? AvatarUrl);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
