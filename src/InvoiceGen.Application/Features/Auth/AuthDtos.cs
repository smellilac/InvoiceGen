using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Auth;

public sealed record RegisterRequest(string Email, string Password, string? BusinessName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UpdateUserRequest(
    string? BusinessName,
    string? BusinessAddress,
    string? LogoUrl,
    string? DefaultCurrency);

public sealed record AppUserDto(
    Guid Id,
    string Email,
    string? BusinessName,
    string? BusinessAddress,
    string? LogoUrl,
    string? DefaultCurrency,
    DateTimeOffset CreatedAt)
{
    public static AppUserDto FromEntity(AppUser user) => new(
        user.Id,
        user.Email!,
        user.BusinessName,
        user.BusinessAddress,
        user.LogoUrl,
        user.DefaultCurrency,
        user.CreatedAt);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    AppUserDto User);
