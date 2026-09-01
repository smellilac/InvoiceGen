using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Auth;

public interface IJwtTokenGenerator
{
    (string Token, int ExpiresInSeconds) GenerateAccessToken(AppUser user);
}
