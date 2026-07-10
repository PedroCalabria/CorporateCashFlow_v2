using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Application.Interfaces;

/// <summary>
/// The request-scoped identity every capability reads to apply RBAC, populated from the
/// validated JWT claims (design.md §D1). For an unauthenticated request it reports
/// <see cref="IsAuthenticated"/> = <c>false</c> rather than throwing.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    UserRole? Role { get; }

    /// <summary><c>null</c> = global scope; otherwise the subsidiary the current user is tied to.</summary>
    Guid? SubsidiaryId { get; }
}
