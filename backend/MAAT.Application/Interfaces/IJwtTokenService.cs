using MAAT.Domain.Entities;

namespace MAAT.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}
