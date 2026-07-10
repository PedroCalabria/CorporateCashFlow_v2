namespace CorporateTreasury.Application.DTOs.Auth;

/// <summary>
/// The token pair produced by a successful login or refresh. The controller returns
/// <see cref="AccessToken"/> in the body and writes <see cref="RefreshToken"/> (raw)
/// into the <c>HttpOnly</c> refresh cookie, using <see cref="RefreshTokenExpiresAt"/>
/// for the cookie lifetime.
/// </summary>
public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);
