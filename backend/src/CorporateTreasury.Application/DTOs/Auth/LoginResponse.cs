namespace CorporateTreasury.Application.DTOs.Auth;

/// <summary>
/// Login/refresh response body. Only the access token travels in the body; the refresh
/// token is delivered out-of-band in an <c>HttpOnly</c> cookie (design.md §D3).
/// </summary>
public sealed record LoginResponse(string AccessToken);
