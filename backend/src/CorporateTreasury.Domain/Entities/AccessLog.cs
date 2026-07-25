using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// An immutable security-trail record (docs/requirements-document.md §3.9;
/// docs/business-rules-formalization.md §5): a login success/failure, or a <c>403 Forbidden</c>
/// response caught by the global <c>AccessDeniedLoggingMiddleware</c> (design.md §D3).
/// <see cref="UserId"/> is nullable — null for a failed login against an email that matches no
/// user, or for an <see cref="AccessLogEventType.AccessDenied"/> event on a request that never
/// authenticated at all.
/// </summary>
public class AccessLog
{
    private AccessLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public AccessLogEventType EventType { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTime Timestamp { get; private set; }

    public static AccessLog Create(Guid? userId, AccessLogEventType eventType, string? ipAddress) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
        };
}
