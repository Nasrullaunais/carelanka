using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

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

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "getBillingRates")]
    [ProducesResponseType(typeof(BillingRateBook), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<BillingRateBook>> GetBillingRates(CancellationToken ct)
        => Ok(await _rates.GetBookAsync(ct));

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
