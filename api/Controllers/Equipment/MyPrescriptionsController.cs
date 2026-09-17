using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

// Under /me because it is the patient's own view, but owned by Equipment: the pharmacy is ours.
[ApiController]
[Route("api/me/prescriptions")]
[Tags("Pharmacy")]
[Authorize(Policy = Policies.PatientOnly)]
public class MyPrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptions;

    public MyPrescriptionsController(IPrescriptionService prescriptions) => _prescriptions = prescriptions;

    [HttpGet(Name = "listMyPrescriptions")]
    [ProducesResponseType(typeof(List<MyPrescription>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<MyPrescription>>> ListMyPrescriptions(CancellationToken ct)
        => Ok(await _prescriptions.ListMineAsync(ct));

    [HttpPost(Name = "uploadMyPrescription")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MyPrescription), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyPrescription>> UploadMyPrescription(
        [FromForm] UploadPrescriptionRequest request, CancellationToken ct)
    {
        var prescription = await _prescriptions.UploadMineAsync(request, ct);

        return CreatedAtRoute("listMyPrescriptions", null, prescription);
    }
}
