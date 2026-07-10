using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IUserRepository"/>.</summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _db.Users.AddAsync(user, cancellationToken);
}
