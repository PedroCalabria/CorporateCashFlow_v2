namespace CorporateTreasury.Application.Interfaces;

/// <summary>
/// Password hashing abstraction owned by Application; implemented in Infrastructure by
/// wrapping <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;T&gt;</c> (PBKDF2) in
/// isolation — no full Identity stack (design.md §D2).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Verify <paramref name="password"/> against a stored PBKDF2 <paramref name="hash"/>.</summary>
    bool Verify(string hash, string password);
}
