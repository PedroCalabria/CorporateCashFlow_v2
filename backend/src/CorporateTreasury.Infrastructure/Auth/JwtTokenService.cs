using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CorporateTreasury.Infrastructure.Auth;

/// <summary>
/// Issues signed JWT access tokens and cryptographically strong opaque refresh tokens
/// (design.md §D3). The <c>subsidiaryId</c> claim is emitted <b>only</b> when the user is
/// subsidiary-scoped; its absence marks global scope. Refresh tokens are opaque random
/// values — only their SHA-256 hash is ever persisted.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    /// <summary>Claim type carrying the user's role; matched by the JwtBearer RoleClaimType in Api.</summary>
    public const string RoleClaimType = "role";

    /// <summary>Claim type carrying the subsidiary scope; present only for scoped users.</summary>
    public const string SubsidiaryClaimType = "subsidiaryId";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(RoleClaimType, user.Role.ToString()),
        };

        // Emit subsidiaryId ONLY when non-null — absence = global scope (spec requirement).
        if (user.SubsidiaryId is Guid subsidiaryId)
        {
            claims.Add(new Claim(SubsidiaryClaimType, subsidiaryId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        return new GeneratedRefreshToken(rawToken, HashRefreshToken(rawToken), expiresAt);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
