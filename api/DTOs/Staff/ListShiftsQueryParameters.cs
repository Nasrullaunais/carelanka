using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class ListShiftsQueryParameters
{
    [FromQuery(Name = "wardId")]
    public Guid? WardId { get; set; }

    [FromQuery(Name = "from")]
    public DateOnly From { get; set; }

    [FromQuery(Name = "to")]
    public DateOnly To { get; set; }

    [FromQuery(Name = "role")]
    public StaffRole? Role { get; set; }

    [FromQuery(Name = "coverageStatus")]
    public CoverageStatus? CoverageStatus { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;

    [FromQuery(Name = "sortBy")]
    public string SortBy { get; set; } = "date";

    [FromQuery(Name = "sortDir")]
    public string SortDir { get; set; } = "asc";
}
