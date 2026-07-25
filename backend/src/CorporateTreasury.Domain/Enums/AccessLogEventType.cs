namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// Events recorded in the <c>AccessLog</c> security trail (docs/requirements-document.md §3.9;
/// docs/business-rules-formalization.md §5).
/// </summary>
public enum AccessLogEventType
{
    LoginSuccess,
    LoginFailed,

    /// <summary>Any request that received a <c>403 Forbidden</c> response, logged by the global middleware (design.md §D3).</summary>
    AccessDenied,
}
