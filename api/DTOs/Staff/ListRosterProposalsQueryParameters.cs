using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class ListRosterProposalsQueryParameters
{
    [FromQuery(Name = "status")]
    public RosterProposalStatus? Status { get; set; }

    [FromQuery(Name = "wardId")]
    public Guid? WardId { get; set; }

    [FromQuery(Name = "shiftId")]
    public Guid? ShiftId { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
