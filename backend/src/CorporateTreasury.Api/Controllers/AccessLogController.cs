using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.AuditTrail;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateTreasury.Api.Controllers;

/// <summary>
/// The "Access Activity" tab's backing endpoint (audit-trail capability). Any authenticated role
/// may reach the controller (<c>[Authorize]</c>); <see cref="AccessLogQueryService"/> enforces that
/// only a Manager or Auditor may read, scoped to their own subsidiary or global.
/// </summary>
[ApiController]
[Route("api/access-log")]
[Authorize]
public sealed class AccessLogController : ControllerBase
{
    private readonly AccessLogQueryService _service;

    public AccessLogController(AccessLogQueryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new AccessLogFilter(eventType, userId, dateFrom, dateTo);
            var result = await _service.ListAsync(filter, new PagedRequest(page, pageSize), cancellationToken);
            return Ok(result);
        }
        catch (ForbiddenOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
