using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/patient-accounts")]
[Tags("Patient app accounts")]
[Authorize(Policy = Policies.PatientPasswordReset)]
public class PatientAppAccountsController : ControllerBase
{
    private readonly IPatientService _patients;

    public PatientAppAccountsController(IPatientService patients) => _patients = patients;

    [HttpGet(Name = "listPatientAppAccounts")]
    [ProducesResponseType(typeof(PagedResult<PatientAppAccount>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PatientAppAccount>>> ListPatientAppAccounts(
        [FromQuery] PatientAppAccountListRequest request, CancellationToken ct)
        => Ok(await _patients.ListAppAccountsAsync(request, ct));

    [HttpPost("{patientId:guid}/reset-password", Name = "resetPatientAppPassword")]
    [ProducesResponseType(typeof(PatientAppPasswordReset), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PatientAppPasswordReset>> ResetPatientAppPassword(
        Guid patientId, CancellationToken ct)
    {
        var reset = await _patients.ResetAppPasswordAsync(patientId, ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(reset);
    }
}
