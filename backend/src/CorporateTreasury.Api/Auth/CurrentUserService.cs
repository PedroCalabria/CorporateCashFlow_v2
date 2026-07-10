using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Infrastructure.Auth;

namespace CorporateTreasury.Api.Auth;

/// <summary>
/// HTTP-backed <see cref="ICurrentUserService"/>. Reads the validated JWT claims off the
/// current request's <see cref="ClaimsPrincipal"/> via <see cref="IHttpContextAccessor"/>.
/// Lives in Api (the only project that legitimately references <c>HttpContext</c>) even
/// though the interface is owned by Application — see design.md §D1.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirst(JwtTokenService.RoleClaimType)?.Value, out var role)
            ? role
            : null;

    public Guid? SubsidiaryId =>
        Guid.TryParse(Principal?.FindFirst(JwtTokenService.SubsidiaryClaimType)?.Value, out var id)
            ? id
            : null;
}
