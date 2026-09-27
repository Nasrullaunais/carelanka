using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Patient;

public sealed class PatientAppAccountListRequest
{
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;
}
