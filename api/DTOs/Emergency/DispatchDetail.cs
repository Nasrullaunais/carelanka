namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchDetail : CallDispatch
{
    public string? SceneAddressLabel { get; set; }
    public string? SceneDetails { get; set; }
    public decimal SceneLatitude { get; set; }
    public decimal SceneLongitude { get; set; }
    public decimal SceneLocationAccuracyMetres { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public bool PatientIsCaller { get; set; }
    public IReadOnlyList<Guid> CrewStaffIds { get; set; } = [];
}
