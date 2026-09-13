using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/maintenance-schedules")]
[Tags("Maintenance")]
public class MaintenanceSchedulesController : ControllerBase
{
    private readonly IMaintenanceService _maintenance;

    public MaintenanceSchedulesController(IMaintenanceService maintenance)
        => _maintenance = maintenance;

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

        return Created((string?)null, schedule);
    }

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
