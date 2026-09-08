using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Tests;

[ApiController]
[Route("api/test/policy")]
public sealed class TestPolicyController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = nameof(StaffRole.DutyManager))]
    public IActionResult Get() => Ok();
}
