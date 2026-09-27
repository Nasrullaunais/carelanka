using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IStaffReportsService
{
    Task<CoverageReport> GetCoverageReportAsync(
        CoverageReportParameters parameters,
        CancellationToken cancellationToken = default);

    Task<LeaveReport> GetLeaveReportAsync(
        LeaveReportParameters parameters,
        CancellationToken cancellationToken = default);

    Task<StaffAgentPerformanceReport> GetStaffAgentPerformanceReportAsync(
        StaffAgentPerformanceReportParameters parameters,
        CancellationToken cancellationToken = default);
}
