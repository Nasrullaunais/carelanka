using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class AmbulanceListRequest
{
    [FromQuery(Name = "status")]
    public AmbulanceStatus? Status { get; set; }

    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    [FromQuery(Name = "nearToLatitude")]
    public decimal? NearToLatitude { get; set; }

    [FromQuery(Name = "nearToLongitude")]
    public decimal? NearToLongitude { get; set; }

    [FromQuery(Name = "includeRetired")]
    public bool IncludeRetired { get; set; }

    [FromQuery(Name = "eligibleOnly")]
    public bool EligibleOnly { get; set; }

    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;

    [FromQuery(Name = "sortBy")]
    public AmbulanceSortField SortBy { get; set; } = AmbulanceSortField.RegistrationNumber;

    [FromQuery(Name = "sortDir")]
    public string SortDir { get; set; } = "desc";
}
