namespace CorporateTreasury.Application.DTOs.AuditTrail;

/// <summary>Optional filters for <c>GET /api/audit-log</c> (applied on top of the caller's enforced scope).</summary>
public sealed record AuditLogFilter(
    string? EntityType = null,
    string? Action = null,
    Guid? PerformedBy = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

/// <summary>
/// Read model for an <c>AuditLog</c> row (docs/business-rules-formalization.md §5).
/// <see cref="PerformedByName"/> is resolved from <c>Users</c> for display (falls back to
/// <c>"System"</c> for the reserved automatic-match actor, <see cref="CorporateTreasury.Domain.Common.SystemActor"/>) —
/// <see cref="PerformedBy"/> itself remains the raw id.
/// </summary>
public sealed record AuditLogResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid PerformedBy,
    string PerformedByName,
    DateTime PerformedAt,
    string? OldValue,
    string? NewValue);

/// <summary>Optional filters for <c>GET /api/access-log</c> (applied on top of the caller's enforced scope).</summary>
public sealed record AccessLogFilter(
    string? EventType = null,
    Guid? UserId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

/// <summary>
/// Read model for an <c>AccessLog</c> row (docs/requirements-document.md §3.9).
/// <see cref="UserName"/> is resolved from <c>Users</c> for display; null when <see cref="UserId"/>
/// is null (a failed login against an email that matched no user).
/// </summary>
public sealed record AccessLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string EventType,
    string? IpAddress,
    DateTime Timestamp);
