namespace CareLanka.Api.DTOs.Emergency;

public sealed class ReportAmbulanceLocationRequest
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
