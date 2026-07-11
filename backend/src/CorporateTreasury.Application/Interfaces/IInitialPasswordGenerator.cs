namespace CorporateTreasury.Application.Interfaces;

/// <summary>
/// Produces the one-time initial password handed back (once) when a Manager creates a user.
/// Implemented in Infrastructure with a cryptographically strong RNG. Kept behind an interface so
/// the generation policy is swappable and the service stays testable (design.md §D3).
/// </summary>
public interface IInitialPasswordGenerator
{
    string Generate();
}
