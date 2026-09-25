using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class StaffAgentPerformanceReportParameters
{
    [FromQuery(Name = "from")]
    public DateOnly? From { get; set; }

    [FromQuery(Name = "to")]
    public DateOnly? To { get; set; }
}
