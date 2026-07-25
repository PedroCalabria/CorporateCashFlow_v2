using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// A scoped, filtered, paginated query for access-log rows. <see cref="ScopeSubsidiaryId"/> is the
/// caller's enforced scope (null = global, sees everything including unattributed failed logins);
/// a subsidiary-scoped caller is resolved via the row's <c>User.SubsidiaryId</c> and never sees a
/// row with a null <see cref="AccessLog.UserId"/> (design.md §D5).
/// </summary>
public sealed record AccessLogQuery(
    Guid? ScopeSubsidiaryId,
    AccessLogEventType? EventType,
    Guid? UserId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int Skip,
    int Take);

/// <summary>
/// Persistence for <see cref="AccessLog"/> rows. Implemented by Infrastructure (EF Core). Unlike
/// <see cref="IAuditLogRepository"/>, this repository owns its own <see cref="SaveChangesAsync"/>:
/// an <see cref="AccessLog"/> write is sometimes the only change in its unit of work (the global
/// 403-logging middleware has no other aggregate to piggyback on), not always paired with another
/// aggregate's save the way audit rows are (design.md §D1/§D3).
/// </summary>
public interface IAccessLogRepository
{
    Task AddAsync(AccessLog accessLog, CancellationToken cancellationToken = default);

    /// <summary>Returns the matching page of rows plus the total count for that filter/scope.</summary>
    Task<(IReadOnlyList<AccessLog> Items, int TotalCount)> ListAsync(AccessLogQuery query, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
