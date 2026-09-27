using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Emergency;
using CareLanka.Api.Services.Patient;
using Xunit;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;

namespace CareLanka.Api.Tests;

public sealed class PreAdmissionGatewayTests
{
    [Fact]
    public async Task A_request_is_mapped_to_patient_management()
    {
        var admissionService = new StubAdmissionService();
        var gateway = new PreAdmissionGateway(admissionService);
        var dispatchId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var expectedArrival = DateTimeOffset.UtcNow.AddMinutes(30);

        var outcome = await gateway.SendAsync(new PreAdmissionRequest(
            dispatchId,
            callerUserId,
            true,
            patientId,
            expectedArrival,
            AdmissionUrgency.Emergency));

        Assert.Equal(PreAdmissionOutcome.Created, outcome);
        var request = Assert.IsType<PreAdmitRequest>(admissionService.Request);
        Assert.Equal(dispatchId.ToString(), request.DispatchId);
        Assert.True(request.PatientIsCaller);
        Assert.Equal(callerUserId, request.CallerUserId);
        Assert.Equal(patientId, request.PatientId);
        Assert.Equal(expectedArrival, request.ExpectedArrival);
        Assert.Equal(AdmissionUrgency.Emergency, request.Urgency);
        Assert.Null(request.DestinationWardTypeHint);
    }

    [Fact]
    public async Task The_same_dispatch_is_the_only_conflict_that_counts_as_sent()
    {
        var duplicate = new PreAdmissionGateway(new StubAdmissionService(
            new ConflictException(MessageCode.PreAdmissionAlreadyExists, "dispatch")));
        var openAdmission = new PreAdmissionGateway(new StubAdmissionService(
            new ConflictException(MessageCode.PatientHasOpenAdmission, "patient")));

        Assert.Equal(PreAdmissionOutcome.AlreadyExists, await duplicate.SendAsync(Request()));
        Assert.Equal(PreAdmissionOutcome.Rejected, await openAdmission.SendAsync(Request()));
    }

    [Fact]
    public async Task A_missing_patient_is_rejected()
    {
        var gateway = new PreAdmissionGateway(new StubAdmissionService(
            new NotFoundException("Patient", Guid.NewGuid())));

        Assert.Equal(PreAdmissionOutcome.Rejected, await gateway.SendAsync(Request()));
    }

    [Fact]
    public async Task An_unavailable_patient_service_is_left_for_the_processor_to_retry()
    {
        var gateway = new PreAdmissionGateway(new StubAdmissionService(
            new InvalidOperationException("Patient Management unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync(Request()));
    }

    private static PreAdmissionRequest Request() => new(
        Guid.NewGuid(),
        null,
        false,
        null,
        DateTimeOffset.UtcNow.AddMinutes(30),
        AdmissionUrgency.Urgent);

    private sealed class StubAdmissionService(Exception? exception = null) : IAdmissionService
    {
        public PreAdmitRequest? Request { get; private set; }

        public Task<AdmissionResponse> PreAdmitAsync(
            PreAdmitRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return exception is null
                ? Task.FromResult<AdmissionResponse>(null!)
                : Task.FromException<AdmissionResponse>(exception);
        }

        public Task<PagedResult<AdmissionSummary>> ListAsync(
            IReadOnlyCollection<AdmissionStatus>? statuses,
            AdmissionCategory? category,
            AdmissionSource? source,
            bool? detailsComplete,
            string? search,
            int page,
            int pageSize,
            AdmissionSortField sortBy,
            SortDirection sortDir,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AdmissionDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> CreateAsync(
            CreateAdmissionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> ClassifyAsync(
            Guid id, ClassifyAdmissionRequest request, Guid staffId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> CompleteDetailsAsync(
            Guid id, CompleteDetailsRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> MarkArrivedAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionResponse> CancelAsync(
            Guid id, CancelAdmissionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
