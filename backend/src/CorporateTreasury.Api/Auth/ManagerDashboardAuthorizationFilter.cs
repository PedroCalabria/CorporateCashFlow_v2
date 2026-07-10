using CorporateTreasury.Domain.Enums;
using Hangfire.Dashboard;

namespace CorporateTreasury.Api.Auth;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated <see cref="UserRole.Manager"/>s,
/// replacing the temporary <c>AllowAllDashboardAuthorizationFilter</c> now that auth exists
/// (docs/technical-architecture.md §5, item 1). Authorization reads the request principal's
/// role claim, which JwtBearer is configured to treat as the role claim type.
/// </summary>
public sealed class ManagerDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true
            && user.IsInRole(nameof(UserRole.Manager));
    }
}
