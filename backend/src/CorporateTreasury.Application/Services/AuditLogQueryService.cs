using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.AuditTrail;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Common;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// The scoped, paginated read over <see cref="AuditLog"/> for the <c>audit-trail</c> capability's
/// "Ledger Activity" tab (design.md §D4). A <c>Manager</c> or <c>Auditor</c> may call this; an
/// <c>Editor</c> is rejected with <see cref="ForbiddenOperationException"/> (→ 403). Resolves
/// <c>PerformedBy</c> to a display name via <see cref="IUserRepository"/> — a raw Guid isn't
/// useful to a human reviewing the trail (fixed after initial delivery: the reserved automatic-
/// match actor, <see cref="SystemActor"/>, isn't a real <c>User</c> row and is displayed as "System").
/// </summary>
public sealed class AuditLogQueryService
{
    private const string SystemDisplayName = "System";
    private const string UnknownUserDisplayName = "Unknown user";

    private readonly IAuditLogRepository _auditLogs;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;

    public AuditLogQueryService(IAuditLogRepository auditLogs, IUserRepository users, ICurrentUserService currentUser)
    {
        _auditLogs = auditLogs;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogResponse>> ListAsync(AuditLogFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        EnsureReader();

        var query = new AuditLogQuery(
            ScopeSubsidiaryId: _currentUser.SubsidiaryId,
            EntityType: filter.EntityType,
            Action: ParseAction(filter.Action),
            PerformedBy: filter.PerformedBy,
            DateFrom: filter.DateFrom,
            DateTo: filter.DateTo,
            Skip: paging.Skip,
            Take: paging.Take);

        var (items, total) = await _auditLogs.ListAsync(query, cancellationToken);

        var performerIds = items.Select(a => a.PerformedBy).Where(id => id != SystemActor.Id).Distinct().ToList();
        var performerNames = (await _users.GetByIdsAsync(performerIds, cancellationToken))
            .ToDictionary(u => u.Id, u => u.Name);

        return new PagedResult<AuditLogResponse>(
            items.Select(a => ToResponse(a, performerNames)).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    private void EnsureReader()
    {
        if (_currentUser.Role != UserRole.Manager && _currentUser.Role != UserRole.Auditor)
        {
            throw new ForbiddenOperationException("Only a Manager or Auditor can access the audit trail.");
        }
    }

    private static AuditAction? ParseAction(string? action) =>
        Enum.TryParse<AuditAction>(action, ignoreCase: true, out var parsed) ? parsed : null;

    private static AuditLogResponse ToResponse(AuditLog a, IReadOnlyDictionary<Guid, string> performerNames)
    {
        var performedByName = a.PerformedBy == SystemActor.Id
            ? SystemDisplayName
            : performerNames.GetValueOrDefault(a.PerformedBy, UnknownUserDisplayName);

        return new AuditLogResponse(
            a.Id,
            a.EntityType,
            a.EntityId,
            a.Action.ToString(),
            a.PerformedBy,
            performedByName,
            a.PerformedAt,
            a.OldValue,
            a.NewValue);
    }
}
