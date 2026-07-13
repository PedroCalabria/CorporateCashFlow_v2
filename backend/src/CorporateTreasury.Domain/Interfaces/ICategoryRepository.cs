using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>Read access to the fixed <see cref="Category"/> catalog. Implemented by Infrastructure (EF Core).</summary>
public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
