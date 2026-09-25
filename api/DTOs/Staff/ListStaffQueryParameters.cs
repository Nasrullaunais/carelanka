using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Staff;

public class ListStaffQueryParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    [FromQuery(Name = "role")]
    public StaffRole? Role { get; set; }

    [FromQuery(Name = "skill")]
    public Guid? Skill { get; set; }

    [FromQuery(Name = "department")]
    public string? Department { get; set; }

    [FromQuery(Name = "includeInactive")]
    public bool IncludeInactive { get; set; } = false;

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;

    [FromQuery(Name = "sortBy")]
    public string SortBy { get; set; } = "full_name";

    [FromQuery(Name = "sortDir")]
    public string SortDir { get; set; } = "asc";
}
