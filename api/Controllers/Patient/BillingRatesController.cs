using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>
/// What the hospital charges. Read by anyone who shows a price, written by the administrator.
/// </summary>
/// <remarks>
/// The read and the write are deliberately on different policies. Every role at the billing
/// desk needs the suggested price in the box in front of them; setting the price is a decision
/// about what the hospital charges, and that is the administrator's alone.
/// </remarks>
[ApiController]
[Route("api/billing/rates")]
[Tags("Billing")]
public class BillingRatesController : ControllerBase
{
    private readonly IBillingRateService _rates;

    public BillingRatesController(IBillingRateService rates)
    {
        _rates = rates;
    }

    /// <summary>The whole price grid: every expense, in every kind of ward, plus the admission fees.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "getBillingRates")]
    [ProducesResponseType(typeof(BillingRateBook), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<BillingRateBook>> GetBillingRates(CancellationToken ct)
        => Ok(await _rates.GetBookAsync(ct));

    /// <summary>
    /// Change prices. Only the cells that changed need to be sent.
    /// </summary>
    /// <remarks>
    /// <b>This never alters a bill already raised.</b> The price is copied onto the line when
    /// the line is written, so a change here prices tomorrow's bills and leaves every piece of
    /// paper a patient has already been handed exactly as it was.
    /// </remarks>
    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPut(Name = "updateBillingRates")]
    [ProducesResponseType(typeof(BillingRateBook), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<BillingRateBook>> UpdateBillingRates(
        [FromBody] UpdateBillingRatesRequest request, CancellationToken ct)
        => Ok(await _rates.UpdateAsync(request, ct));
}
