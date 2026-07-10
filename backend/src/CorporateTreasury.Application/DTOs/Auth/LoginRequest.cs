namespace CorporateTreasury.Application.DTOs.Auth;

/// <summary>Credentials posted to <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);
