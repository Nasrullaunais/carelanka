using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class ListLeaveRequestsQueryParameters
{
    [FromQuery(Name = "staffMemberId")]
    public Guid? StaffMemberId { get; set; }

    [FromQuery(Name = "status")]
    public LeaveStatus? Status { get; set; }

    [FromQuery(Name = "type")]
    public LeaveType? Type { get; set; }

    [FromQuery(Name = "from")]
    public DateOnly? From { get; set; }

    [FromQuery(Name = "to")]
    public DateOnly? To { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
