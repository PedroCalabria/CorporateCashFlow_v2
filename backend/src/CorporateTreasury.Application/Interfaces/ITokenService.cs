using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Application.Interfaces;

/// <summary>
/// Issues access tokens (JWT) and opaque refresh tokens. Implemented in Infrastructure
/// (<c>JwtTokenService</c>). The <c>subsidiaryId</c> claim is emitted only when non-null
/// (design.md §D3); refresh tokens are opaque and only their hash is ever persisted.
/// </summary>
public interface ITokenService
{
    /// <summary>Issue a short-lived signed JWT carrying <c>sub</c>, <c>role</c>, and (when scoped) <c>subsidiaryId</c>.</summary>
    string CreateAccessToken(User user);

    /// <summary>Generate a cryptographically strong opaque refresh token plus its hash and expiry.</summary>
    GeneratedRefreshToken GenerateRefreshToken();

    /// <summary>Hash a presented raw refresh token the same way as <see cref="GenerateRefreshToken"/>, for store lookup.</summary>
    string HashRefreshToken(string rawToken);
}

/// <summary>
/// The output of generating a refresh token: the raw value goes to the client cookie,
/// only the <see cref="TokenHash"/> is stored server-side.
/// </summary>
public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAt);
