using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data.Enums;
using Xunit;

namespace CareLanka.Api.Tests.Staff;

[Collection(ApiCollection.Name)]
public sealed class StaffNotificationTests
{
    private readonly NotificationTestKit _kit;

    public StaffNotificationTests(ApiApplication application) => _kit = new NotificationTestKit(application);

    [Fact]
    public async Task Asking_for_leave_alerts_every_other_duty_manager()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var managerId = await _kit.StaffIdAsync(ApiApplication.ManagerEmail);
        await _kit.SeedStaffAsync(StaffRole.DutyManager);

        var leaveId = await RequestLeaveAsync(manager);

        await _kit.AssertSentAsync(NotificationType.LeaveRequested, leaveId,
            staff: await _kit.RoleAsync(StaffRole.DutyManager, managerId), patientAccounts: [],
            actorStaffId: managerId);
    }

    [Fact]
    public async Task Approving_leave_tells_the_person_who_asked()
    {
        var (requesterId, requesterEmail) = await _kit.SeedStaffAsync(StaffRole.WardNurse);
        using var requester = await _kit.StaffAsync(requesterEmail);
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var leaveId = await RequestLeaveAsync(requester);

        var decided = await manager.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);

        await _kit.AssertSentAsync(NotificationType.LeaveApproved, leaveId,
            staff: [requesterId], patientAccounts: [],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task Rejecting_leave_tells_the_person_who_asked()
    {
        var (requesterId, requesterEmail) = await _kit.SeedStaffAsync(StaffRole.WardNurse);
        using var requester = await _kit.StaffAsync(requesterEmail);
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var leaveId = await RequestLeaveAsync(requester);

        var decided = await manager.PostAsJsonAsync(
            $"/api/leave-requests/{leaveId}/decision", new { decision = "reject", notes = "Too many people away." });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);

        await _kit.AssertSentAsync(NotificationType.LeaveRejected, leaveId,
            staff: [requesterId], patientAccounts: [],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task Rostering_someone_onto_a_shift_tells_that_person()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var (nurseId, _) = await _kit.SeedStaffAsync(StaffRole.WardNurse);
        var shiftId = await NewShiftAsync();

        var allocationId = await AllocateAsync(manager, shiftId, nurseId);

        await _kit.AssertSentAsync(NotificationType.ShiftChanged, allocationId,
            staff: [nurseId], patientAccounts: [],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task Taking_someone_off_a_shift_tells_that_person_again()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var (nurseId, _) = await _kit.SeedStaffAsync(StaffRole.WardNurse);
        var allocationId = await AllocateAsync(manager, await NewShiftAsync(), nurseId);

        var ended = await manager.PostAsJsonAsync(
            $"/api/allocations/{allocationId}/end", new { reason = "manual", suppress_agent = true });
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);

        Assert.Equal(2, await _kit.CountAsync(NotificationType.ShiftChanged, allocationId));
    }

    [Fact]
    public async Task A_roster_proposal_waiting_for_approval_alerts_the_other_hospital_administrators()
    {
        using var administrator = await _kit.StaffAsync(ApiApplication.AdministratorEmail);
        var administratorId = await _kit.StaffIdAsync(ApiApplication.AdministratorEmail);
        await _kit.SeedStaffAsync(StaffRole.HospitalAdministrator);
        await _kit.SeedStaffAsync(StaffRole.WardNurse);
        var shiftId = await NewShiftAsync();

        var proposed = await administrator.PostAsJsonAsync("/api/roster-proposals", new { shift_id = shiftId });
        Assert.Equal(HttpStatusCode.Accepted, proposed.StatusCode);
        using var body = JsonDocument.Parse(await proposed.Content.ReadAsStringAsync());
        Assert.Equal("pending_approval", body.RootElement.GetProperty("status").GetString());

        await _kit.AssertSentAsync(NotificationType.RosterProposalWaiting, body.RootElement.GetProperty("id").GetGuid(),
            staff: await _kit.RoleAsync(StaffRole.HospitalAdministrator, administratorId), patientAccounts: [],
            actorStaffId: administratorId);
    }

    private static async Task<Guid> RequestLeaveAsync(HttpClient staff)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Random.Shared.Next(60, 3000)));

        var created = await staff.PostAsJsonAsync("/api/me/leave-requests", new
        {
            type = "annual",
            start_date = start.ToString("yyyy-MM-dd"),
            end_date = start.AddDays(2).ToString("yyyy-MM-dd"),
            reason = "Family visit"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return await IdAsync(created);
    }

    private async Task<Guid> NewShiftAsync()
    {
        using var administrator = await _kit.StaffAsync(ApiApplication.AdministratorEmail);
        var ward = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name = $"Notify-Roster-{Guid.NewGuid():N}"[..24],
            ward_type = "general",
            gender_policy = "mixed",
            is_active = true
        });
        Assert.Equal(HttpStatusCode.Created, ward.StatusCode);

        var shift = await administrator.PostAsJsonAsync("/api/shifts", new
        {
            ward_id = await IdAsync(ward),
            date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Random.Shared.Next(60, 3000))).ToString("yyyy-MM-dd"),
            start_time = "08:00",
            end_time = "16:00",
            required_role = "ward_nurse",
            headcount_needed = 1,
            minimum_headcount = 1
        });
        Assert.Equal(HttpStatusCode.Created, shift.StatusCode);

        return await IdAsync(shift);
    }

    private static async Task<Guid> AllocateAsync(HttpClient manager, Guid shiftId, Guid staffId)
    {
        var allocated = await manager.PostAsJsonAsync("/api/allocations", new
        {
            shift_id = shiftId,
            staff_member_id = staffId
        });
        Assert.Equal(HttpStatusCode.Created, allocated.StatusCode);

        return await IdAsync(allocated);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }
}
