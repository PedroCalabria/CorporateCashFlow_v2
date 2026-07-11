using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CorporateTreasury.Api.Auth;

/// <summary>Named authorization policies used across the Api.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Only the Global Manager — <c>Manager</c> role with no subsidiary scope (null
    /// <c>subsidiaryId</c>) — may pass. A subsidiary-scoped token (any role) is forbidden.
    /// </summary>
    public const string GlobalManager = "GlobalManager";
}

/// <summary>Authorization requirement satisfied only by the Global Manager (design.md §D2).</summary>
public sealed class GlobalManagerRequirement : IAuthorizationRequirement;

/// <summary>
/// Authorizes the <see cref="AuthorizationPolicies.GlobalManager"/> policy by reading the
/// request-scoped identity (<see cref="ICurrentUserService"/>) rather than re-parsing claims.
/// Succeeds only for a <c>Manager</c> with a null <c>subsidiaryId</c>; combined with
/// <c>RequireAuthenticatedUser</c> this yields <c>401</c> for anonymous requests and <c>403</c>
/// for any subsidiary-scoped user — on every HTTP method the controller exposes.
/// </summary>
public sealed class GlobalManagerHandler : AuthorizationHandler<GlobalManagerRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public GlobalManagerHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GlobalManagerRequirement requirement)
    {
        if (_currentUser.Role == UserRole.Manager && _currentUser.SubsidiaryId is null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
