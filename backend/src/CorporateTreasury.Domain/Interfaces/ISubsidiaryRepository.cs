using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// Read/write access to <see cref="Subsidiary"/> aggregates (with their <c>BankAccount</c>).
/// Implemented by Infrastructure (EF Core); the interface lives in Domain so Application
/// depends only on the abstraction.
/// </summary>
public interface ISubsidiaryRepository
{
    /// <summary>Loads a subsidiary with its bank account, or <c>null</c> if not found.</summary>
    Task<Subsidiary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All subsidiaries (active and inactive), each with its bank account.</summary>
    Task<IReadOnlyList<Subsidiary>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>True if another subsidiary already uses <paramref name="code"/> (optionally excluding one id).</summary>
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Number of active users currently assigned to this subsidiary (deactivation guard input).</summary>
    Task<int> CountActiveUsersAsync(Guid subsidiaryId, CancellationToken cancellationToken = default);

    Task AddAsync(Subsidiary subsidiary, CancellationToken cancellationToken = default);

    /// <summary>Persist pending changes (creation, edits, activation flips).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
