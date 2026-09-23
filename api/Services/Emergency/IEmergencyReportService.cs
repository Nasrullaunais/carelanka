using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IEmergencyReportService
{
    Task<ResponseTimeReport> GetResponseTimesAsync(DateOnly from, DateOnly to, CallPriority? priority,
        CancellationToken cancellationToken = default);
    Task<FleetUtilisationReport> GetFleetUtilisationAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
    Task<EmergencyAgentPerformanceReport> GetAgentPerformanceAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}
