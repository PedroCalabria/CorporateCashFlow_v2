using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace CorporateTreasury.Api.Auth;

/// <summary>Authorization requirement satisfied by any <c>Manager</c>, global or subsidiary-scoped.</summary>
public sealed class ManagerRequirement : IAuthorizationRequirement;

/// <summary>
/// Authorizes the <see cref="AuthorizationPolicies.Manager"/> policy by reading the request-scoped
/// identity (<see cref="ICurrentUserService"/>). Succeeds for any <c>Manager</c> regardless of
/// scope; combined with <c>RequireAuthenticatedUser</c> this yields <c>401</c> for anonymous
/// requests and <c>403</c> for <c>Editor</c>/<c>Auditor</c> — on every method the controller exposes.
/// The Global-vs-Subsidiary scope asymmetry is applied afterwards in the Application service.
/// </summary>
public sealed class ManagerHandler : AuthorizationHandler<ManagerRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public ManagerHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManagerRequirement requirement)
    {
        if (_currentUser.Role == UserRole.Manager)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
