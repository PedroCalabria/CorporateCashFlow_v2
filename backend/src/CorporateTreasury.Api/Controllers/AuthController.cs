using CorporateTreasury.Application.DTOs.Auth;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Application.Services;
using CorporateTreasury.Domain.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// Authentication endpoints. Login/refresh are anonymous; logout requires a valid access
/// token. The refresh token travels only in an <c>HttpOnly</c> + <c>SameSite</c> cookie
/// (<c>Secure</c> over HTTPS); the access token is returned in the body (design.md §D3/§D4).
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Cookie carrying the opaque refresh token. Scoped to the auth path so it is sent only where needed.</summary>
    private const string RefreshCookieName = "refreshToken";
    private const string RefreshCookiePath = "/api/auth";

    private const string InvalidCredentialsMessage = "Invalid email or password.";

    private readonly AuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _users;

    public AuthController(
        AuthService authService,
        IValidator<LoginRequest> loginValidator,
        ICurrentUserService currentUser,
        IUserRepository users)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _currentUser = currentUser;
        _users = users;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var tokens = await _authService.LoginAsync(request, cancellationToken);
        if (tokens is null)
        {
            // Same generic 401 for unknown email, wrong password, and inactive account.
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        SetRefreshCookie(tokens);
        return Ok(new LoginResponse(tokens.AccessToken));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var presented = Request.Cookies[RefreshCookieName];
        var tokens = await _authService.RefreshAsync(presented, cancellationToken);
        if (tokens is null)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        SetRefreshCookie(tokens);
        return Ok(new LoginResponse(tokens.AccessToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        // UserId is guaranteed present here: [Authorize] already rejected unauthenticated
        // requests, and every issued token carries `sub`.
        var userId = _currentUser.UserId!.Value;
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role.ToString(),
            user.SubsidiaryId));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var presented = Request.Cookies[RefreshCookieName];
        await _authService.LogoutAsync(presented, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    private void SetRefreshCookie(AuthTokens tokens)
    {
        Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken, BuildCookieOptions(tokens.RefreshTokenExpiresAt));
    }

    private void ClearRefreshCookie()
    {
        // Expire immediately with matching attributes so the browser drops it.
        Response.Cookies.Append(RefreshCookieName, string.Empty, BuildCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        // Secure only when the request is actually HTTPS, so the cookie still works over
        // plain-HTTP localhost in Docker dev (design.md Open Questions).
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = RefreshCookiePath,
        Expires = expires,
    };
}
