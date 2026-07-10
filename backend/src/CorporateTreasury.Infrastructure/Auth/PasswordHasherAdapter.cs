using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CorporateTreasury.Infrastructure.Auth;

/// <summary>
/// Implements the Application-owned <see cref="IPasswordHasher"/> by wrapping
/// <see cref="PasswordHasher{TUser}"/> (PBKDF2) from <c>Microsoft.Extensions.Identity.Core</c>,
/// used in isolation — no <c>UserManager</c>, no Identity EF schema (design.md §D2).
/// </summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    // The generic parameter is only a type marker for the underlying hasher; the User
    // instance is irrelevant to PBKDF2, so a throwaway is passed to the API.
    private static readonly User Marker = new()
    {
        Name = string.Empty,
        Email = string.Empty,
        PasswordHash = string.Empty,
    };

    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Marker, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(Marker, hash, password) != PasswordVerificationResult.Failed;
}
