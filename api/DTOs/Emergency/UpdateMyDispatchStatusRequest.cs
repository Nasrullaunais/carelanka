using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class UpdateMyDispatchStatusRequest
{
    public DispatchStatus? Status { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
