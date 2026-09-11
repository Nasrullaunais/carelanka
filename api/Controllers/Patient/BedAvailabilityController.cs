using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>Which beds are free. Equipment Management's register, joined with our assignments.</summary>
/// <remarks>
/// Deliberately not <c>/api/beds</c> - that route belongs to Equipment Management, which owns
/// the register itself. Theirs lists the beds that exist; this one answers which of them can
/// be used. One app cannot have two pages at the same address, so the two have to differ.
/// </remarks>
[ApiController]
[Route("api/bed-availability")]
[Tags("Wards and Beds")]
public class BedAvailabilityController : ControllerBase
{
    private readonly IBedAssignmentService _assignments;

    public BedAvailabilityController(IBedAssignmentService assignments)
        => _assignments = assignments;

    /// <summary>
    /// List beds with their availability. This is the candidate list the bed agent will work
    /// from, and the one a nurse picks from by hand today.
    /// </summary>
    /// <remarks>
    /// `free` applies hold expiry, so a bed whose 30-minute reservation has lapsed is reported
    /// free with nobody having released it. That expiry rule lives in one place in this
    /// component and is not re-implemented per endpoint.
    ///
    /// Retired wards and retired beds are absent rather than listed as unavailable.
    /// </remarks>
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
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _assignments.ListAvailabilityAsync(
            wardId, wardType, availability, needsIsolation, page, pageSize, ct));
}
