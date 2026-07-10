using Hangfire.Dashboard;

namespace CorporateTreasury.Api;

/// <summary>
/// Grants access to the Hangfire dashboard to everyone.
/// <para>
/// TEMPORARY for the project-bootstrap change. Hangfire's default filter only
/// allows local requests, which blocks access through the Docker port mapping.
/// This is replaced by a Manager-only <c>IDashboardAuthorizationFilter</c> once
/// authentication exists (see docs/technical-architecture.md §5, item 1).
/// </para>
/// </summary>
public sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
