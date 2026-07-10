using System.IdentityModel.Tokens.Jwt;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace CorporateTreasury.UnitTests.Auth;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "unit-test-signing-key-that-is-long-enough-0123456789",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        }));

    private static JwtSecurityToken Decode(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Global_user_token_omits_the_subsidiaryId_claim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Global Manager",
            Email = "manager@example.com",
            PasswordHash = "x",
            Role = UserRole.Manager,
            SubsidiaryId = null,
        };

        var jwt = Decode(CreateService().CreateAccessToken(user));

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(nameof(UserRole.Manager), jwt.Claims.Single(c => c.Type == JwtTokenService.RoleClaimType).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenService.SubsidiaryClaimType);
    }

    [Fact]
    public void Subsidiary_scoped_user_token_carries_the_subsidiaryId_claim()
    {
        var subsidiaryId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Scoped Editor",
            Email = "editor@example.com",
            PasswordHash = "x",
            Role = UserRole.Editor,
            SubsidiaryId = subsidiaryId,
        };

        var jwt = Decode(CreateService().CreateAccessToken(user));

        Assert.Equal(subsidiaryId.ToString(), jwt.Claims.Single(c => c.Type == JwtTokenService.SubsidiaryClaimType).Value);
        Assert.Equal(nameof(UserRole.Editor), jwt.Claims.Single(c => c.Type == JwtTokenService.RoleClaimType).Value);
    }

    [Fact]
    public void GenerateRefreshToken_and_HashRefreshToken_are_consistent_and_opaque()
    {
        var service = CreateService();

        var generated = service.GenerateRefreshToken();

        Assert.NotEqual(generated.RawToken, generated.TokenHash); // only the hash is stored
        Assert.Equal(generated.TokenHash, service.HashRefreshToken(generated.RawToken));
        Assert.True(generated.ExpiresAt > DateTime.UtcNow);
    }
}
