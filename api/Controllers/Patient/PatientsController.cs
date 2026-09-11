using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientResponse = CareLanka.Api.DTOs.Patient.Patient;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>The patient register. One record per person, reused across every visit.</summary>
[ApiController]
[Route("api/patients")]
[Tags("Patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patients;

    public PatientsController(IPatientService patients) => _patients = patients;

    /// <summary>
    /// Search patients. `search` matches patient code, full name, NIC, phone or temporary
    /// reference. Equipment Management reads this to find a patient's code.
    /// </summary>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet(Name = "listPatients")]
    [ProducesResponseType(typeof(PagedResult<PatientSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PatientSummary>>> ListPatients(
        [FromQuery] string? search,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] PatientSortField sortBy = PatientSortField.CreatedAt,
        [FromQuery] SortDirection sortDir = SortDirection.Desc,
        CancellationToken ct = default)
        => Ok(await _patients.ListAsync(search, page, pageSize, sortBy, sortDir, ct));

    /// <summary>Get one patient with their visit history. One patient, many admissions.</summary>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet("{id:guid}", Name = "getPatient")]
    [ProducesResponseType(typeof(PatientDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PatientDetail>> GetPatient(Guid id, CancellationToken ct)
        => Ok(await _patients.GetDetailAsync(id, ct));

    /// <summary>
    /// Register a patient. Call lookup first when an NIC is available — a returning patient must
    /// keep one record with many admissions, not gain a second identity.
    /// </summary>
    [Authorize(Policy = Policies.PatientRegistrar)]
    [HttpPost(Name = "createPatient")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PatientResponse>> CreatePatient(
        [FromBody] CreatePatientRequest request, CancellationToken ct)
    {
        var patient = await _patients.CreateAsync(request, ct);

        return CreatedAtRoute("getPatient", new { id = patient.Id }, patient);
    }

    /// <summary>Update a patient record. A full replace — a field left out is cleared.</summary>
    [Authorize(Policy = Policies.PatientEditor)]
    [HttpPut("{id:guid}", Name = "updatePatient")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PatientResponse>> UpdatePatient(
        Guid id, [FromBody] UpdatePatientRequest request, CancellationToken ct)
        => Ok(await _patients.UpdateAsync(id, request, ct));

    /// <summary>
    /// Find an existing patient by NIC before registering a new one. A miss is a 200 with
    /// found = false; not knowing someone is the normal answer at a registration desk.
    /// </summary>
    [Authorize(Policy = Policies.PatientRegistrar)]
    [HttpPost("lookup", Name = "lookupPatient")]
    [ProducesResponseType(typeof(PatientLookupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PatientLookupResult>> LookupPatient(
        [FromBody] PatientLookupRequest request, CancellationToken ct)
        => Ok(await _patients.LookupByNicAsync(request.Nic, ct));

    /// <summary>
    /// Attach a patient login to an existing record. A record and an account are different
    /// things — staff link them deliberately, after checking identity.
    /// </summary>
    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/link-account", Name = "linkPatientAccount")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> LinkPatientAccount(
        Guid id, [FromBody] LinkPatientAccountRequest request, CancellationToken ct)
    {
        await _patients.LinkAccountAsync(id, request.UserAccountId, ct);

        return NoContent();
    }
}
