using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.AuditTrail;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// The scoped, paginated read over <see cref="AccessLog"/> for the <c>audit-trail</c> capability's
/// "Access Activity" tab (design.md §D4). A <c>Manager</c> or <c>Auditor</c> may call this; an
/// <c>Editor</c> is rejected with <see cref="ForbiddenOperationException"/> (→ 403). Resolves
/// <c>UserId</c> to a display name via <see cref="IUserRepository"/> — a raw Guid isn't useful to a
/// human reviewing the trail (fixed after initial delivery).
/// </summary>
public sealed class AccessLogQueryService
{
    private const string UnknownUserDisplayName = "Unknown user";

    private readonly IAccessLogRepository _accessLogs;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;

    public AccessLogQueryService(IAccessLogRepository accessLogs, IUserRepository users, ICurrentUserService currentUser)
    {
        _accessLogs = accessLogs;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AccessLogResponse>> ListAsync(AccessLogFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        EnsureReader();

        var query = new AccessLogQuery(
            ScopeSubsidiaryId: _currentUser.SubsidiaryId,
            EventType: ParseEventType(filter.EventType),
            UserId: filter.UserId,
            DateFrom: filter.DateFrom,
            DateTo: filter.DateTo,
            Skip: paging.Skip,
            Take: paging.Take);

        var (items, total) = await _accessLogs.ListAsync(query, cancellationToken);

        var userIds = items.Select(a => a.UserId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var userNames = (await _users.GetByIdsAsync(userIds, cancellationToken))
            .ToDictionary(u => u.Id, u => u.Name);

        return new PagedResult<AccessLogResponse>(
            items.Select(a => ToResponse(a, userNames)).ToList(),
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

    private static AccessLogEventType? ParseEventType(string? eventType) =>
        Enum.TryParse<AccessLogEventType>(eventType, ignoreCase: true, out var parsed) ? parsed : null;

    private static AccessLogResponse ToResponse(AccessLog a, IReadOnlyDictionary<Guid, string> userNames)
    {
        var userName = a.UserId is Guid userId ? userNames.GetValueOrDefault(userId, UnknownUserDisplayName) : null;

        return new AccessLogResponse(
            a.Id,
            a.UserId,
            userName,
            a.EventType.ToString(),
            a.IpAddress,
            a.Timestamp);
    }
}
