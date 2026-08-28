using Praxis.Domain.Entities;

namespace Praxis.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
