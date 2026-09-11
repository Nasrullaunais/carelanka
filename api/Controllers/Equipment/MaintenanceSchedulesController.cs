using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>Servicing, calibration and repairs, for equipment items and beds alike.</summary>
[ApiController]
[Route("api/maintenance-schedules")]
[Tags("Maintenance")]
public class MaintenanceSchedulesController : ControllerBase
{
    private readonly IMaintenanceService _maintenance;

    public MaintenanceSchedulesController(IMaintenanceService maintenance)
        => _maintenance = maintenance;

    /// <summary>
    /// The work list, soonest first. `overdue=true` returns tasks still scheduled whose date
    /// has passed, worked out when you ask rather than stored, so nothing has to sweep the
    /// table at midnight to keep it honest.
    /// </summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpGet(Name = "listMaintenanceSchedules")]
    [ProducesResponseType(typeof(PagedResult<MaintenanceSchedule>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MaintenanceSchedule>>> ListMaintenanceSchedules(
        [FromQuery] MaintenanceStatus? status,
        [FromQuery] AssetType? assetType,
        [FromQuery] bool? overdue,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _maintenance.ListAsync(
            new MaintenanceQuery(status, assetType, overdue, page, pageSize), ct));

    /// <summary>
    /// Book a service by hand, with no agent involved. This path has to keep working: if the
    /// only way to schedule maintenance were through the agent, the hospital would stop the
    /// day the agent did. Booking against an occupied bed is refused with a 409.
    /// </summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createMaintenanceSchedule")]
    [ProducesResponseType(typeof(MaintenanceSchedule), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MaintenanceSchedule>> CreateMaintenanceSchedule(
        [FromBody] CreateMaintenanceScheduleRequest request, CancellationToken ct)
    {
        var schedule = await _maintenance.CreateAsync(request, ct);

        // No Location header: the contract publishes no endpoint that reads one schedule.
        return Created((string?)null, schedule);
    }

    /// <summary>
    /// Mark the work done. The asset goes back into service, its next service is booked
    /// forward, and any warning that led here closes, all in one transaction. Who did the
    /// work comes from the token, never the body.
    /// </summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/complete", Name = "completeMaintenanceSchedule")]
    [ProducesResponseType(typeof(MaintenanceSchedule), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MaintenanceSchedule>> CompleteMaintenanceSchedule(
        Guid id, [FromBody] CompleteMaintenanceScheduleRequest? request, CancellationToken ct)
        => Ok(await _maintenance.CompleteAsync(id, request?.Notes, ct));
}
