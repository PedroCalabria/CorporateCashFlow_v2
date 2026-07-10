namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// A persisted, server-side refresh token used to renew short-lived access tokens.
/// Only the <see cref="TokenHash"/> is stored (never the opaque token itself), so a DB
/// leak does not yield usable tokens. Rotation makes each token single-use: refreshing
/// revokes the presented token and links it to its replacement — see design.md §D3.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hash of the opaque refresh token value; the raw value lives only in the client cookie.</summary>
    public required string TokenHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the token is revoked (via rotation or logout); a revoked token is permanently unusable.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>On rotation, points to the token that superseded this one.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>True while the token is neither revoked nor expired — the only state that may be refreshed.</summary>
    public bool IsActive => RevokedAt is null && !IsExpired;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
