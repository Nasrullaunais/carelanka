using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public static class DispatchStatusExtensions
{
    public static readonly DispatchStatus[] LiveStatuses =
    [
        DispatchStatus.Assigned, DispatchStatus.Acknowledged, DispatchStatus.EnRouteToScene,
        DispatchStatus.AtScene, DispatchStatus.TransportingToHospital
    ];

    public static bool IsLive(this DispatchStatus status) => status is
        DispatchStatus.Assigned or DispatchStatus.Acknowledged or DispatchStatus.EnRouteToScene
        or DispatchStatus.AtScene or DispatchStatus.TransportingToHospital;

    public static bool IsPrePickup(this DispatchStatus status) => status is
        DispatchStatus.Assigned or DispatchStatus.Acknowledged or DispatchStatus.EnRouteToScene;

    public static bool IsAcknowledgementOverdue(this Dispatch dispatch, DateTimeOffset now, int timeoutSeconds)
        => dispatch.Status == DispatchStatus.Assigned && now - dispatch.DispatchedAt >= TimeSpan.FromSeconds(timeoutSeconds);
}
