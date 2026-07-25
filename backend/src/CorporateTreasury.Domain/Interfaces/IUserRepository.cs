using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// Read/write access to <see cref="User"/> aggregates. Implemented by Infrastructure
/// (EF Core); the interface lives in Domain so Application depends only on the abstraction.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup by id — used to resolve a display name for a set of actor ids (e.g. the audit-trail capability's PerformedBy/UserId columns).</summary>
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Users visible to the caller. When <paramref name="scopeSubsidiaryId"/> is null the caller is
    /// global-scoped and all users are returned; otherwise only users of that subsidiary.
    /// </summary>
    Task<IReadOnlyList<User>> ListAsync(Guid? scopeSubsidiaryId, CancellationToken cancellationToken = default);

    /// <summary>True if a user with this email already exists (case-insensitive lookup key is enforced by the store).</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Persist pending changes (creation, edits, activation flips, password resets).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
