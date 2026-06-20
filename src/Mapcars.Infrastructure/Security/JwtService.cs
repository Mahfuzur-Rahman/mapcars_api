using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Mapcars.Infrastructure.Security;

public class JwtService(IConfiguration config) : IJwtService
{
    public int ExpiryMinutes => int.TryParse(config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

    public string GenerateToken(Admin admin)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured. Set it via user-secrets.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(JwtRegisteredClaimNames.Name,  admin.FullName),
            new Claim(ClaimTypes.Role,               admin.Role?.Name ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateUserToken(Guid userId, string? identifier, string userType)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim("identifier",                  identifier ?? string.Empty),
            new Claim("user_type",                   userType),
            new Claim(ClaimTypes.Role,               userType),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
