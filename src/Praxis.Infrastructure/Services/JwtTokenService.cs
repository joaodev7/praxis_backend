using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Praxis.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secret = _configuration["JWT:KEY"] ?? _configuration["Jwt:Secret"] ?? _configuration["Jwt:Key"] ?? "PraxisDevelopmentSecretKeyChangeThis123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("TenantId", user.TenantId.ToString())
        };

        if (user.NutritionistProfile != null)
        {
            claims.Add(new Claim("NutritionistId", user.NutritionistProfile.Id.ToString()));
        }

        var expiresMinutes = int.TryParse(_configuration["Jwt:ExpiryInMinutes"], out var mins) ? mins : 1440; // 24h default
        var issuer = _configuration["JWT:ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "Praxis";
        var audience = _configuration["JWT:AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "Praxis";

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
