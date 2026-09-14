using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/bed-availability")]
[Tags("Wards and Beds")]
public class BedAvailabilityController : ControllerBase
{
    private readonly IBedAssignmentService _assignments;

    public BedAvailabilityController(IBedAssignmentService assignments)
        => _assignments = assignments;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listBedAvailability")]
    [ProducesResponseType(typeof(PagedResult<AdmissionBed>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AdmissionBed>>> ListBedAvailability(
        [FromQuery] Guid? wardId,
        [FromQuery] WardType? wardType,
        [FromQuery] bool? needsIsolation,
        [FromQuery] BedAvailabilityFilter availability = BedAvailabilityFilter.All,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 500)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _assignments.ListAvailabilityAsync(
            wardId, wardType, availability, needsIsolation, page, pageSize, ct));
}
