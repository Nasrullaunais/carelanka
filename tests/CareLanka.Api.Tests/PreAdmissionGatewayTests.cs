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

    [Fact]
    public async Task A_withdrawal_cancels_the_admission_found_by_dispatch_with_the_given_reason()
    {
        var dispatchId = Guid.NewGuid();
        var admission = new AdmissionEntity { Id = Guid.NewGuid(), Status = AdmissionStatus.AwaitingBed };
        var admissions = new StubAdmissionService { Existing = admission };

        var outcome = await new PreAdmissionGateway(admissions).WithdrawAsync(dispatchId, CancelReason.DiedAtScene);

        Assert.Equal(PreAdmissionWithdrawalOutcome.Withdrawn, outcome);
        Assert.Equal(dispatchId.ToString(), admissions.LookedUpDispatchId);
        var cancelled = Assert.Single(admissions.Cancellations);
        Assert.Equal(admission.Id, cancelled.Id);
        Assert.Equal(CancelReason.DiedAtScene, cancelled.Request.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AdmissionStatus.Cancelled)]
    public async Task A_missing_or_already_cancelled_admission_needs_no_withdrawal(AdmissionStatus? status)
    {
        var admissions = new StubAdmissionService
        {
            Existing = status is null ? null : new AdmissionEntity { Id = Guid.NewGuid(), Status = status.Value }
        };

        var outcome = await new PreAdmissionGateway(admissions).WithdrawAsync(Guid.NewGuid(), CancelReason.FalseAlarm);

        Assert.Equal(PreAdmissionWithdrawalOutcome.Withdrawn, outcome);
        Assert.Empty(admissions.Cancellations);
    }

    [Fact]
    public async Task A_refused_cancellation_is_rejected_and_a_vanished_admission_is_withdrawn()
    {
        var admission = new AdmissionEntity { Id = Guid.NewGuid(), Status = AdmissionStatus.Admitted };
        var refused = new StubAdmissionService
        {
            Existing = admission,
            CancelFailure = new IllegalTransitionException("Admission", "admitted", "cancelled")
        };
        var vanished = new StubAdmissionService
        {
            Existing = admission,
            CancelFailure = new NotFoundException("Admission", admission.Id)
        };

        Assert.Equal(
            PreAdmissionWithdrawalOutcome.Rejected,
            await new PreAdmissionGateway(refused).WithdrawAsync(Guid.NewGuid(), CancelReason.FalseAlarm));
        Assert.Equal(
            PreAdmissionWithdrawalOutcome.Withdrawn,
            await new PreAdmissionGateway(vanished).WithdrawAsync(Guid.NewGuid(), CancelReason.FalseAlarm));
    }

    [Fact]
    public async Task An_unavailable_patient_service_is_left_for_the_processor_to_retry_when_withdrawing()
    {
        var admissions = new StubAdmissionService
        {
            Existing = new AdmissionEntity { Id = Guid.NewGuid(), Status = AdmissionStatus.AwaitingBed },
            CancelFailure = new InvalidOperationException("Patient Management unavailable")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PreAdmissionGateway(admissions).WithdrawAsync(Guid.NewGuid(), CancelReason.FalseAlarm));
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

        public AdmissionEntity? Existing { get; init; }

        public Exception? CancelFailure { get; init; }

        public string? LookedUpDispatchId { get; private set; }

        public List<(Guid Id, CancelAdmissionRequest Request)> Cancellations { get; } = [];

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
        {
            Cancellations.Add((id, request));
            return CancelFailure is null
                ? Task.FromResult<AdmissionResponse>(null!)
                : Task.FromException<AdmissionResponse>(CancelFailure);
        }

        public Task<AdmissionEntity?> FindByDispatchIdAsync(string dispatchId, CancellationToken cancellationToken = default)
        {
            LookedUpDispatchId = dispatchId;
            return Task.FromResult(Existing);
        }

        public Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
