using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class ListAllocationsQueryParameters
{
    [FromQuery(Name = "shiftId")]
    public Guid? ShiftId { get; set; }

    [FromQuery(Name = "staffMemberId")]
    public Guid? StaffMemberId { get; set; }

    [FromQuery(Name = "wardId")]
    public Guid? WardId { get; set; }

    [FromQuery(Name = "status")]
    public AllocationStatus? Status { get; set; }

    [FromQuery(Name = "from")]
    public DateOnly? From { get; set; }

    [FromQuery(Name = "to")]
    public DateOnly? To { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}