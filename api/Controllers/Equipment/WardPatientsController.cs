using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/ward-patients")]
[Tags("Laboratory")]
public class WardPatientsController : ControllerBase
{
    private readonly IWardPatientService _patients;

    public WardPatientsController(IWardPatientService patients) => _patients = patients;

    [Authorize(Policy = Policies.PatientLocationReader)]
    [HttpGet(Name = "listWardPatients")]
    [ProducesResponseType(typeof(PagedResult<WardPatient>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<WardPatient>>> ListWardPatients(
        [FromQuery] string? wardName,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _patients.ListAsync(wardName, page, pageSize, ct));
}
