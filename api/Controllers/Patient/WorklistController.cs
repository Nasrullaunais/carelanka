using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>The ward board: who the hospital is dealing with, across bookings and visits at once.</summary>
[ApiController]
[Route("api/patient-worklist")]
[Tags("Admissions")]
public class WorklistController : ControllerBase
{
    private readonly IWorklistService _worklist;

    public WorklistController(IWorklistService worklist) => _worklist = worklist;

    /// <summary>
    /// One page of the ward board, newest first. `search` matches the patient's code, name or NIC.
    /// </summary>
    /// <remarks>
    /// **Two tables, one list.** A row is either a booking nobody has checked in yet -
    /// `kind = booking`, `status = not_arrived` - or a visit that has started, `kind = visit`.
    /// A booking that has been checked in appears once, as its visit, never twice.
    ///
    /// This exists because `GET /admissions` cannot answer the question. An admission is
    /// created by arriving, so a list of admissions can never say "not arrived" about anybody,
    /// and the patient booked in for a scan at eleven was invisible until she walked in.
    ///
    /// **Read-only, and derived.** `status` is a reading of `AppointmentStatus` or
    /// `AdmissionStatus`, not a fourth stored status. Nothing transitions between its values;
    /// every write still goes to `/appointments/{id}/check-in`, `/admissions/{id}/assign-bed`
    /// and the rest. So there is no `PATCH` here and there never will be.
    ///
    /// No `sortBy`. It is a worklist read top to bottom, not a report - and one sort key over a
    /// union of two tables whose time columns mean different things would order it by a column
    /// nobody asked for.
    /// </remarks>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet(Name = "listPatientWorklist")]
    [ProducesResponseType(typeof(PagedResult<WorklistRow>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<WorklistRow>>> ListPatientWorklist(
        [FromQuery] string? search,
        [FromQuery] bool includeFinished = false,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _worklist.ListAsync(search, includeFinished, page, pageSize, ct));
}
