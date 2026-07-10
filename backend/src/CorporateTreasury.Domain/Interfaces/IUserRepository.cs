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

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
