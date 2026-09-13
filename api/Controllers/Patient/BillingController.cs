using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api")]
[Tags("Billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingController(IBillingService billing)
    {
        _billing = billing;
    }

    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet("admissions/{admissionId:guid}/bill", Name = "getAdmissionBill")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> GetAdmissionBill(
        Guid admissionId, CancellationToken ct)
        => Ok(await _billing.GetAsync(admissionId, ct));

    [Authorize(Policy = Policies.BillingDesk)]
    [HttpPost("admissions/{admissionId:guid}/bill", Name = "prepareAdmissionBill")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> PrepareAdmissionBill(
        Guid admissionId, CancellationToken ct)
        => Ok(await _billing.PrepareAsync(admissionId, ct));

    [Authorize(Policy = Policies.BillingDesk)]
    [HttpPost("admissions/{admissionId:guid}/bill/charges", Name = "addBillCharge")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> AddBillCharge(
        Guid admissionId, [FromBody] AddBillChargeRequest request, CancellationToken ct)
        => Ok(await _billing.AddChargeAsync(admissionId, request, ct));

    [Authorize(Policy = Policies.BillingDesk)]
    [HttpDelete("admissions/{admissionId:guid}/bill/charges/{lineId:guid}", Name = "removeBillCharge")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> RemoveBillCharge(
        Guid admissionId, Guid lineId, CancellationToken ct)
        => Ok(await _billing.RemoveChargeAsync(admissionId, lineId, ct));

    [Authorize(Policy = Policies.BillingDesk)]
    [HttpPost("admissions/{admissionId:guid}/bill/settle", Name = "settleBill")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> SettleBill(
        Guid admissionId, [FromBody] SettleBillRequest? request, CancellationToken ct)
        => Ok(await _billing.SettleAsync(admissionId, request ?? new SettleBillRequest(), ct));

    [Authorize(Policy = Policies.BillingDesk)]
    [HttpGet("billing/outstanding", Name = "listOutstandingBills")]
    [ProducesResponseType(typeof(PagedResult<OutstandingBill>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<OutstandingBill>>> ListOutstandingBills(
        [FromQuery] string? search,
        [FromQuery] bool includeSettled = false,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _billing.ListOutstandingAsync(search, includeSettled, page, pageSize, ct));
}
