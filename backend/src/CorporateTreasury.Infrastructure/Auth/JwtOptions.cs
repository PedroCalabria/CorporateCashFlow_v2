namespace CorporateTreasury.Infrastructure.Auth;

/// <summary>
/// JWT signing/lifetime settings, bound from the <c>Jwt</c> configuration section. The
/// signing key, issuer, and audience come from configuration so they differ per
/// environment (design.md §D3).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric signing key (HMAC-SHA256). Must be long enough for HS256 (≥ 32 bytes).</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes (~15).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token lifetime in days (~7).</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
