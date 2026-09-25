using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IStaffReportsService
{
    Task<CoverageReport> GetCoverageReportAsync(
        CoverageReportParameters parameters,
        CancellationToken cancellationToken = default);
}
