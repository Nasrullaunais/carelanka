using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCallListRequest
{
    public CallStatus? Status { get; set; }
    public CallPriority? Priority { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool UnassignedOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public EmergencyCallSortField SortBy { get; set; } = EmergencyCallSortField.Priority;
    public string SortDir { get; set; } = "desc";
}
