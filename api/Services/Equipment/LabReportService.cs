using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using ReportEntity = CareLanka.Api.Data.Entities.Equipment.LabReport;

namespace CareLanka.Api.Services.Equipment;

public sealed class LabReportService : ILabReportService
{
    public const int MaxBytes = 10 * 1024 * 1024;

    // An allow-list rather than a block-list: the question is whether a ward can open it.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private readonly CareLankaDbContext _db;
    private readonly IPatientDirectory _patients;
    private readonly ICurrentUser _currentUser;

    public LabReportService(
        CareLankaDbContext db,
        IPatientDirectory patients,
        ICurrentUser currentUser)
    {
        _db = db;
        _patients = patients;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<LabReport>> ListForPatientAsync(
        Guid patientId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var reports = _db.LabReports.AsNoTracking().Where(r => r.PatientId == patientId);

        var totalItems = await reports.CountAsync(cancellationToken);

        // The projection leaves Content out deliberately. Selecting the whole entity would read
        // every stored PDF out of the database to render a table of filenames.
        var rows = await reports
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new LabReport
            {
                Id = r.Id,
                PatientId = r.PatientId,
                TestName = r.TestName,
                Summary = r.Summary,
                FileName = r.FileName,
                ContentType = r.ContentType,
                ByteSize = r.ByteSize,
                UploadedByStaffId = r.UploadedByStaffId,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<LabReport>.From(rows, page, pageSize, totalItems);
    }

    public async Task<LabReport> UploadAsync(
        UploadLabReportRequest request, CancellationToken cancellationToken = default)
    {
        // Asked before anything is read or written. A report filed against a patient who does not
        // exist is worse than a rejected upload: nothing errors, and the ward waits for a result
        // sitting under a mistyped id.
        if (!await _patients.ExistsAsync(request.PatientId, cancellationToken))
        {
            throw new NotFoundException("Patient", request.PatientId);
        }

        var file = request.File;

        if (file.Length <= 0)
        {
            throw new BadRequestException(MessageCode.LabReportFileEmpty);
        }

        if (file.Length > MaxBytes)
        {
            throw new BadRequestException(
                MessageCode.LabReportFileTooLarge, MaxBytes / (1024 * 1024));
        }

        var contentType = file.ContentType?.Split(';')[0].Trim() ?? string.Empty;

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new BadRequestException(MessageCode.LabReportFileType, contentType);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var report = new ReportEntity
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            TestName = request.TestName.Trim(),
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
            // The name only. A browser sends whatever the client machine had, and a stored
            // "C:\Users\..\report.pdf" is both useless and somebody's directory layout.
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            Content = buffer.ToArray(),
            ByteSize = (int)file.Length,
            // From the token, never the body: a caller cannot file a report as somebody else.
            UploadedByStaffId = _currentUser.Id
        };

        _db.LabReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(report);
    }

    public async Task<LabReportFile> GetFileAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _db.LabReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Lab report", id);

        return new LabReportFile(report.Content, report.ContentType, report.FileName);
    }

    private static LabReport ToDto(ReportEntity report) => new()
    {
        Id = report.Id,
        PatientId = report.PatientId,
        TestName = report.TestName,
        Summary = report.Summary,
        FileName = report.FileName,
        ContentType = report.ContentType,
        ByteSize = report.ByteSize,
        UploadedByStaffId = report.UploadedByStaffId,
        CreatedAt = report.CreatedAt
    };
}
