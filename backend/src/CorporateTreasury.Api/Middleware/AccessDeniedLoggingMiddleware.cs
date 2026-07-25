using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Api.Middleware;

/// <summary>
/// Terminal middleware that logs every <c>403 Forbidden</c> response as an <c>AccessLog</c>
/// <see cref="AccessLogEventType.AccessDenied"/> row, regardless of whether the <c>403</c> came
/// from a policy-based authorization handler (<c>ManagerHandler</c>/<c>GlobalManagerHandler</c>,
/// which short-circuits the pipeline without calling <c>next()</c>) or a manual
/// <c>ForbiddenOperationException</c> mapped by a controller's <c>GuardedAsync</c> (design.md §D3).
/// Must be registered <b>before</b> <c>UseAuthentication()</c>/<c>UseAuthorization()</c> in
/// <c>Program.cs</c> — not after — so this middleware's <c>next()</c> call wraps the entire rest of
/// the pipeline; only then does its "after next()" code observe the final status code in both
/// cases. Registering it after <c>UseAuthorization()</c> would mean it never runs at all for a
/// policy-denied request, since that middleware doesn't invoke its own <c>next()</c> on failure.
/// </summary>
public sealed class AccessDeniedLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccessDeniedLoggingMiddleware> _logger;

    public AccessDeniedLoggingMiddleware(RequestDelegate next, ILogger<AccessDeniedLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // IAccessLogRepository/ICurrentUserService are request-scoped; this middleware instance is a
    // singleton, so they are resolved per-request via method injection rather than the constructor.
    public async Task InvokeAsync(HttpContext context, IAccessLogRepository accessLogs, ICurrentUserService currentUser)
    {
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status403Forbidden)
        {
            return;
        }

        try
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            await accessLogs.AddAsync(AccessLog.Create(currentUser.UserId, AccessLogEventType.AccessDenied, ipAddress));
            await accessLogs.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // A failure to persist the access-denied trail must not become a new failure mode for
            // the underlying request, whose response has already been decided (design.md Risks).
            _logger.LogError(ex, "Failed to write an AccessDenied AccessLog row.");
        }
    }
}
