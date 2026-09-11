using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>
/// What a visit costs, and taking the money for it. Reception's screen, not the ward's.
/// </summary>
/// <remarks>
/// Bed days and an admission fee are worked out from what the hospital actually recorded.
/// Everything else is a line somebody at the desk typed, because nothing in this component
/// records a treatment, a procedure or a drug against an admission — see
/// `specs/patient-management-plan.md` §6.5.
/// </remarks>
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

    /// <summary>
    /// The bill for a visit. 404 until somebody prepares one — a bill is written the first time
    /// it is asked for, because a stay that has not happened yet cannot be priced.
    /// </summary>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet("admissions/{admissionId:guid}/bill", Name = "getAdmissionBill")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BillResponse>> GetAdmissionBill(
        Guid admissionId, CancellationToken ct)
        => Ok(await _billing.GetAsync(admissionId, ct));

    /// <summary>
    /// Work the bill out from the stay, and open one if this visit has none.
    /// </summary>
    /// <remarks>
    /// Every generated line is replaced — one admission fee, and one line per bed the patient
    /// has actually been in, at that ward's day rate. Typed charges are left exactly as they
    /// are, so preparing again a day later updates the bed days and keeps the X-ray.
    ///
    /// Refused with 409 once the bill is settled: that is the paper the patient was handed.
    /// </remarks>
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

    /// <summary>
    /// Add a charge reception types in — an X-ray, a dressing pack, a consultant's fee.
    /// </summary>
    /// <remarks>
    /// Typed rather than generated because no table in this component records a treatment
    /// against an admission. Asking a human to type what happened is honest; inventing line
    /// items from tables that do not exist is not.
    /// </remarks>
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

    /// <summary>
    /// Take a typed charge off again. A generated line is refused with 409 — it would come
    /// straight back the next time anyone prepared the bill.
    /// </summary>
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

    /// <summary>
    /// The money is in. Freezes the bill and ticks `billing_settled` on the discharge checklist,
    /// in one transaction.
    /// </summary>
    /// <remarks>
    /// That tick has no other way of being written — `PATCH /discharges/{id}/checklist` refuses
    /// the key. So the bill and the checklist cannot disagree about whether a patient has paid.
    ///
    /// Settling a visit nobody prepared a bill for works the bill out first, so a visit with no
    /// bill can never deadlock a discharge.
    /// </remarks>
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

    /// <summary>
    /// Reception's worklist: visits in the building whose money has not been taken yet.
    /// </summary>
    /// <remarks>
    /// Includes visits with no bill row at all, which is most of them. A list of bills would
    /// have shown an empty screen and left the work invisible.
    ///
    /// A discharged visit is never on the default list: confirming a discharge needs
    /// `billing_settled`, and only settling writes that. `includeSettled=true` is how you reach
    /// one anyway — it widens the list to every visit that has a bill, in any status, which is
    /// what a patient asking for another copy of their bill at the counter needs.
    /// </remarks>
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
