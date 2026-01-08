namespace Ecare.Application.Auth.Responses;

public record AuthResponse(
    string Token,
    string UserName,
    string Email,
    string Role,
    string UserId,
    DateTime ExpiresAt);
