using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/patient-worklist")]
[Tags("Admissions")]
public class WorklistController : ControllerBase
{
    private readonly IWorklistService _worklist;

    public WorklistController(IWorklistService worklist) => _worklist = worklist;

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
