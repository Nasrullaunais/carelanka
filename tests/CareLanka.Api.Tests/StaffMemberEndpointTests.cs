using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class StaffMemberEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/staff", "post", "createStaffMember")]
    [InlineData("/staff", "get", "listStaff")]
    [InlineData("/staff/{id}", "get", "getStaffMember")]
    [InlineData("/staff/{id}", "put", "updateStaffMember")]
    [InlineData("/staff/{id}/deactivate", "post", "deactivateStaffMember")]
    [InlineData("/staff/{id}/reactivate", "post", "reactivateStaffMember")]
    public async Task Staff_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/staff", "post")]
    [InlineData("/staff", "get")]
    [InlineData("/staff/{id}", "get")]
    [InlineData("/staff/{id}", "put")]
    [InlineData("/staff/{id}/deactivate", "post")]
    [InlineData("/staff/{id}/reactivate", "post")]
    public async Task Staff_response_statuses_match_contract(string path, string method)
    {
        using var document = await GenerateSwaggerAsync();
        var contract = LoadContract();
        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

        Assert.True(expected.SetEquals(generated),
            $"{method.ToUpperInvariant()} {path}: contract [{string.Join(", ", expected)}], "
            + $"generated [{string.Join(", ", generated)}]");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task Staff_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();

        var id = Guid.NewGuid();
        var postStaff = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest());
        var getStaff = await client.GetAsync("/api/staff");
        var getStaffMember = await client.GetAsync($"/api/staff/{id}");
        var putStaff = await client.PutAsJsonAsync($"/api/staff/{id}", new UpdateStaffMemberRequest());
        var deactivate = await client.PostAsJsonAsync($"/api/staff/{id}/deactivate", new DeactivateStaffMemberRequest { Reason = "test" });
        var reactivate = await client.PostAsync($"/api/staff/{id}/reactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, postStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getStaffMember.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reactivate.StatusCode);
    }

    [Fact]
    public async Task Staff_endpoints_reject_patient_token()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var id = Guid.NewGuid();
        var postStaff = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest());
        var getStaff = await client.GetAsync("/api/staff");
        var getStaffMember = await client.GetAsync($"/api/staff/{id}");
        var putStaff = await client.PutAsJsonAsync($"/api/staff/{id}", new UpdateStaffMemberRequest());
        var deactivate = await client.PostAsJsonAsync($"/api/staff/{id}/deactivate", new DeactivateStaffMemberRequest { Reason = "test" });
        var reactivate = await client.PostAsync($"/api/staff/{id}/reactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, postStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getStaffMember.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
    }

    [Fact]
    public async Task Staff_admin_only_endpoints_reject_duty_manager()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var id = Guid.NewGuid();
        var postStaff = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest());
        var putStaff = await client.PutAsJsonAsync($"/api/staff/{id}", new UpdateStaffMemberRequest());
        var deactivate = await client.PostAsJsonAsync($"/api/staff/{id}/deactivate", new DeactivateStaffMemberRequest { Reason = "test" });
        var reactivate = await client.PostAsync($"/api/staff/{id}/reactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, postStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
    }

    [Fact]
    public async Task Staff_read_endpoints_allow_duty_manager()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var memberId = Guid.NewGuid();
        stub.AddMember(new StaffMemberDto
        {
            Id = memberId,
            EmployeeNumber = "EMP-12345678",
            FirstName = "Alice",
            LastName = "Smith",
            FullName = "Alice Smith",
            Email = "alice@hospital.invalid",
            Role = StaffRole.WardNurse,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var getStaff = await client.GetAsync("/api/staff");
        var getStaffMember = await client.GetAsync($"/api/staff/{memberId}");

        Assert.Equal(HttpStatusCode.OK, getStaff.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getStaffMember.StatusCode);
    }

    [Fact]
    public async Task Staff_all_endpoints_allow_hospital_administrator()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var memberId = Guid.NewGuid();
        stub.AddMember(new StaffMemberDto
        {
            Id = memberId,
            EmployeeNumber = "EMP-12345678",
            FirstName = "Bob",
            LastName = "Taylor",
            FullName = "Bob Taylor",
            Email = "bob@hospital.invalid",
            Role = StaffRole.Doctor,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var getStaff = await client.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.OK, getStaff.StatusCode);

        var getMember = await client.GetAsync($"/api/staff/{memberId}");
        Assert.Equal(HttpStatusCode.OK, getMember.StatusCode);

        var putMember = await client.PutAsJsonAsync($"/api/staff/{memberId}", new UpdateStaffMemberRequest
        {
            Department = "Pediatrics"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, putMember.StatusCode);

        var deactivate = await client.PostAsJsonAsync($"/api/staff/{memberId}/deactivate", new DeactivateStaffMemberRequest
        {
            Reason = "Contract finished"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var reactivate = await client.PostAsync($"/api/staff/{memberId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
    }

    #endregion

    #region Request Validation Tests

    [Fact]
    public async Task Create_staff_member_rejects_missing_required_fields()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_staff_member_rejects_short_password()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@hospital.invalid",
            TemporaryPassword = "short",
            Role = StaffRole.WardNurse
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_staff_member_rejects_empty_reason()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/staff/{Guid.NewGuid()}/deactivate", new DeactivateStaffMemberRequest
        {
            Reason = ""
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_staff_rejects_invalid_pagination()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/staff?page=0&pageSize=200");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_staff_rejects_invalid_sort_field()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/staff?sortBy=invalid_column");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task Create_staff_member_creates_record_with_employee_number()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var request = new CreateStaffMemberRequest
        {
            FirstName = "Kaveesha",
            LastName = "Rajapaksha",
            Email = "kaveesha.rajapaksha77@carelanka.invalid",
            TemporaryPassword = "SecurePassword123#",
            Role = StaffRole.HospitalAdministrator,
            Department = "Administration",
            PhoneNumber = "+94771234567"
        };

        var response = await client.PostAsJsonAsync("/api/staff", request, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var content = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<StaffMemberDto>(content, JsonOptions);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.StartsWith("EMP-", created.EmployeeNumber);
        Assert.Equal(12, created.EmployeeNumber.Length);
        Assert.Equal("Kaveesha", created.FirstName);
        Assert.Equal("Rajapaksha", created.LastName);
        Assert.Equal("Kaveesha Rajapaksha", created.FullName);
        Assert.Equal("kaveesha.rajapaksha77@carelanka.invalid", created.Email);
        Assert.Equal(StaffRole.HospitalAdministrator, created.Role);
        Assert.True(created.IsActive);
        Assert.DoesNotContain("TemporaryPassword", content);
        Assert.DoesNotContain("PasswordHash", content);
    }

    [Fact]
    public async Task Create_staff_member_rejects_active_email_collision_with_409()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        stub.AddMember(new StaffMemberDto
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = "EMP-AAAAAAAA",
            FirstName = "Active",
            LastName = "User",
            Email = "collision@hospital.invalid",
            Role = StaffRole.WardNurse,
            IsActive = true
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest
        {
            FirstName = "New",
            LastName = "User",
            Email = "collision@hospital.invalid",
            TemporaryPassword = "ValidPassword123#",
            Role = StaffRole.Doctor
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_staff_member_rejects_inactive_email_collision_with_409_and_existing_id()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var inactiveId = Guid.NewGuid();
        stub.AddMember(new StaffMemberDto
        {
            Id = inactiveId,
            EmployeeNumber = "EMP-BBBBBBBB",
            FirstName = "Former",
            LastName = "Employee",
            Email = "former@hospital.invalid",
            Role = StaffRole.WardNurse,
            IsActive = false
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest
        {
            FirstName = "Rehired",
            LastName = "Employee",
            Email = "former@hospital.invalid",
            TemporaryPassword = "ValidPassword123#",
            Role = StaffRole.WardNurse
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("existing_id", out var existingIdProp));
        Assert.Equal(inactiveId, existingIdProp.GetGuid());
    }

    [Fact]
    public async Task Create_staff_member_rejects_invalid_skill_ids_with_400()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var fakeSkillId = Guid.NewGuid();
        stub.InvalidSkillIdsToReject.Add(fakeSkillId);

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff", new CreateStaffMemberRequest
        {
            FirstName = "Staff",
            LastName = "Member",
            Email = "staff@hospital.invalid",
            TemporaryPassword = "ValidPassword123#",
            Role = StaffRole.WardNurse,
            SkillIds = new List<Guid> { fakeSkillId }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("invalid_skill_ids", out var invalidIdsProp));
        Assert.Contains(fakeSkillId.ToString(), invalidIdsProp.ToString());
    }

    [Fact]
    public async Task List_staff_filters_and_paginates()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        stub.AddMember(new StaffMemberDto { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe", FullName = "John Doe", Role = StaffRole.Doctor, Department = "Cardiology", IsActive = true });
        stub.AddMember(new StaffMemberDto { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Smith", FullName = "Jane Smith", Role = StaffRole.WardNurse, Department = "ICU", IsActive = true });
        stub.AddMember(new StaffMemberDto { Id = Guid.NewGuid(), FirstName = "Deactivated", LastName = "Person", FullName = "Deactivated Person", Role = StaffRole.GeneralStaff, Department = "Maintenance", IsActive = false });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = JsonSerializer.Deserialize<PagedResult<StaffSummaryDto>>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalItems);
        Assert.DoesNotContain(page.Items, i => !i.IsActive);

        var responseInactive = await client.GetAsync("/api/staff?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, responseInactive.StatusCode);
        var pageInactive = JsonSerializer.Deserialize<PagedResult<StaffSummaryDto>>(await responseInactive.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(pageInactive);
        Assert.Equal(3, pageInactive.TotalItems);

        var responseRole = await client.GetAsync("/api/staff?role=doctor");
        Assert.Equal(HttpStatusCode.OK, responseRole.StatusCode);
        var pageRole = JsonSerializer.Deserialize<PagedResult<StaffSummaryDto>>(await responseRole.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(pageRole);
        Assert.Single(pageRole.Items);
        Assert.Equal(StaffRole.Doctor, pageRole.Items[0].Role);
    }

    [Fact]
    public async Task Get_staff_member_returns_detail_with_skills_and_allocations()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            EmployeeNumber = "EMP-11223344",
            FirstName = "Sarah",
            LastName = "Connor",
            FullName = "Sarah Connor",
            Email = "sarah@hospital.invalid",
            Role = StaffRole.WardNurse,
            IsActive = true
        });

        stub.SetDetailData(id,
            new List<StaffSkillDto>
            {
                new() { SkillId = skillId, SkillName = "ICU Certified", IsValid = true, GrantedAt = DateTimeOffset.UtcNow }
            },
            new List<AllocationSummaryDto>
            {
                new() { AllocationId = allocId, ShiftId = Guid.NewGuid(), WardName = "ICU", Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), StartTime = "08:00", EndTime = "16:00", StaffMemberId = id, StaffName = "Sarah Connor", Status = AllocationStatus.Confirmed }
            });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/staff/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = JsonSerializer.Deserialize<StaffMemberDetailDto>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(id, detail.Id);
        Assert.Single(detail.Skills);
        Assert.Equal("ICU Certified", detail.Skills[0].SkillName);
        Assert.Single(detail.UpcomingAllocations);
        Assert.Equal("ICU", detail.UpcomingAllocations[0].WardName);
        Assert.Null(detail.LeaveBalanceDays);
    }

    [Fact]
    public async Task Update_staff_member_reports_affected_allocations_on_role_change()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            FirstName = "Mark",
            LastName = "Ruffalo",
            FullName = "Mark Ruffalo",
            Role = StaffRole.WardNurse,
            IsActive = true
        });

        var affectedAlloc = new AllocationSummaryDto
        {
            AllocationId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            WardName = "Emergency Ward",
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartTime = "08:00",
            EndTime = "16:00",
            StaffMemberId = id,
            StaffName = "Mark Ruffalo",
            Status = AllocationStatus.Confirmed
        };
        stub.SetRoleChangeAffectedAllocations(id, StaffRole.GeneralStaff, new List<AllocationSummaryDto> { affectedAlloc });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PutAsJsonAsync($"/api/staff/{id}", new UpdateStaffMemberRequest
        {
            Role = StaffRole.GeneralStaff
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<UpdateStaffMemberResponse>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(StaffRole.GeneralStaff, result.StaffMember.Role);
        Assert.Single(result.AffectedAllocations);
        Assert.Equal("Emergency Ward", result.AffectedAllocations[0].WardName);
    }

    [Fact]
    public async Task Deactivate_staff_member_refuses_with_409_when_future_allocations_exist()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            FirstName = "David",
            LastName = "Tennant",
            FullName = "David Tennant",
            Role = StaffRole.Doctor,
            IsActive = true
        });

        stub.SetDeactivationBlockedWithAllocations(id, new List<AllocationSummaryDto>
        {
            new()
            {
                AllocationId = Guid.NewGuid(),
                ShiftId = Guid.NewGuid(),
                WardName = "Surgical 1",
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                StartTime = "14:00",
                EndTime = "22:00",
                StaffMemberId = id,
                StaffName = "David Tennant",
                Status = AllocationStatus.Confirmed
            }
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/staff/{id}/deactivate", new DeactivateStaffMemberRequest
        {
            Reason = "Left the hospital"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("affected_allocations", out var allocsProp));
        Assert.Equal(JsonValueKind.Array, allocsProp.ValueKind);
        Assert.Equal(1, allocsProp.GetArrayLength());
    }

    [Fact]
    public async Task Deactivate_staff_member_succeeds_when_no_allocations()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            FirstName = "Peter",
            LastName = "Capaldi",
            FullName = "Peter Capaldi",
            Role = StaffRole.Doctor,
            IsActive = true
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/staff/{id}/deactivate", new DeactivateStaffMemberRequest
        {
            Reason = "Retirement"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var member = stub.GetMember(id);
        Assert.NotNull(member);
        Assert.False(member.IsActive);
    }

    [Fact]
    public async Task Reactivate_staff_member_rejects_already_active_with_409()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            FirstName = "Matt",
            LastName = "Smith",
            FullName = "Matt Smith",
            Role = StaffRole.Doctor,
            IsActive = true
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsync($"/api/staff/{id}/reactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_staff_member_succeeds_for_inactive_staff()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubStaffMemberService();
        var id = Guid.NewGuid();

        stub.AddMember(new StaffMemberDto
        {
            Id = id,
            FirstName = "Christopher",
            LastName = "Eccleston",
            FullName = "Christopher Eccleston",
            Role = StaffRole.Doctor,
            IsActive = false
        });

        await using var app = new StaffTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsync($"/api/staff/{id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var member = stub.GetMember(id);
        Assert.NotNull(member);
        Assert.True(member.IsActive);
    }

    #endregion

    #region Helpers and Test Doubles

    private static string CreateToken(string role, string principalType, Guid? subject = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(SigningKey);
        var sub = (subject ?? Guid.NewGuid()).ToString();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(CareLankaClaims.Subject, sub),
                new Claim(CareLankaClaims.Role, role),
                new Claim(CareLankaClaims.PrincipalType, principalType),
                new Claim(CareLankaClaims.TokenId, Guid.NewGuid().ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "carelanka-api",
            Audience = "carelanka-clients",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static async Task<JsonDocument> GenerateSwaggerAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
    }

    private static YamlMappingNode LoadContract()
    {
        using var reader = File.OpenText(Path.Combine(
            AppContext.BaseDirectory, "specs", "staff-spec.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static HashSet<string> Keys(YamlMappingNode node)
        => node.Children.Keys.Cast<YamlScalarNode>().Select(key => key.Value!).ToHashSet();

    private static HashSet<string> Keys(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var part in path)
        {
            current = current.GetProperty(part);
        }

        return current.EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static YamlMappingNode Map(YamlMappingNode root, params string[] path)
        => (YamlMappingNode)Follow(root, path);

    private static YamlNode Follow(YamlNode root, IEnumerable<string> path)
    {
        var current = root;
        foreach (var part in path)
        {
            current = ((YamlMappingNode)current).Children[new YamlScalarNode(part)];
        }

        return current;
    }

    private sealed class SwaggerOnlyApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Development");
    }

    private sealed class StaffTestApplication : WebApplicationFactory<Program>
    {
        private readonly IStaffMemberService? _stub;

        public StaffTestApplication(IStaffMemberService? stub = null)
        {
            _stub = stub;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                if (_stub != null)
                {
                    services.RemoveAll<IStaffMemberService>();
                    services.AddSingleton(_stub);
                }
            });
        }
    }

    private sealed class StubStaffMemberService : IStaffMemberService
    {
        private readonly List<StaffMemberDto> _members = new();
        private readonly Dictionary<Guid, (IReadOnlyList<StaffSkillDto> Skills, IReadOnlyList<AllocationSummaryDto> Allocations)> _detailData = new();
        private readonly Dictionary<(Guid StaffId, StaffRole NewRole), IReadOnlyList<AllocationSummaryDto>> _roleChangeAllocations = new();
        private readonly Dictionary<Guid, IReadOnlyList<AllocationSummaryDto>> _blockedDeactivations = new();
        public List<Guid> InvalidSkillIdsToReject { get; } = new();

        public void AddMember(StaffMemberDto member) => _members.Add(member);
        public StaffMemberDto? GetMember(Guid id) => _members.FirstOrDefault(m => m.Id == id);
        public void SetDetailData(Guid id, IReadOnlyList<StaffSkillDto> skills, IReadOnlyList<AllocationSummaryDto> allocations)
            => _detailData[id] = (skills, allocations);
        public void SetRoleChangeAffectedAllocations(Guid id, StaffRole newRole, IReadOnlyList<AllocationSummaryDto> allocations)
            => _roleChangeAllocations[(id, newRole)] = allocations;
        public void SetDeactivationBlockedWithAllocations(Guid id, IReadOnlyList<AllocationSummaryDto> allocations)
            => _blockedDeactivations[id] = allocations;

        public Task<StaffMemberDto> CreateStaffMemberAsync(CreateStaffMemberRequest request, CancellationToken ct = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var active = _members.FirstOrDefault(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && m.IsActive);
            if (active != null)
            {
                throw new StaffEmailConflictException(null, $"An active staff member already uses the email '{email}'.");
            }

            var inactive = _members.FirstOrDefault(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !m.IsActive);
            if (inactive != null)
            {
                throw new StaffEmailConflictException(inactive.Id, $"An inactive staff member already exists with email '{email}'. Use POST /api/staff/{inactive.Id}/reactivate to reactivate.");
            }

            if (request.SkillIds != null && request.SkillIds.Any(id => InvalidSkillIdsToReject.Contains(id)))
            {
                var invalids = request.SkillIds.Where(id => InvalidSkillIdsToReject.Contains(id)).ToList();
                throw new InvalidSkillsBadRequestException(invalids, $"The following skill IDs do not exist: {string.Join(", ", invalids)}");
            }

            var id = Guid.NewGuid();
            var member = new StaffMemberDto
            {
                Id = id,
                EmployeeNumber = $"EMP-{id.ToString("N")[..8].ToUpperInvariant()}",
                FirstName = request.FirstName,
                LastName = request.LastName,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Email = email,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role,
                Department = request.Department,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _members.Add(member);
            return Task.FromResult(member);
        }

        public Task<PagedResult<StaffSummaryDto>> ListStaffAsync(ListStaffQueryParameters parameters, CancellationToken ct = default)
        {
            var query = _members.AsEnumerable();

            if (!parameters.IncludeInactive)
            {
                query = query.Where(m => m.IsActive);
            }

            if (parameters.Role.HasValue)
            {
                query = query.Where(m => m.Role == parameters.Role.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameters.Department))
            {
                query = query.Where(m => m.Department != null && m.Department.Equals(parameters.Department, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(parameters.Search))
            {
                var term = parameters.Search.Trim().ToLowerInvariant();
                query = query.Where(m => m.FullName.ToLowerInvariant().Contains(term) || m.Email.ToLowerInvariant().Contains(term));
            }

            var list = query.ToList();
            var summaries = list.Select(m => new StaffSummaryDto
            {
                Id = m.Id,
                FullName = m.FullName,
                Role = m.Role,
                Department = m.Department,
                IsActive = m.IsActive,
                SkillCount = 0
            }).ToList();

            var paged = PagedResult<StaffSummaryDto>.From(summaries, parameters.Page, parameters.PageSize, summaries.Count);
            return Task.FromResult(paged);
        }

        public Task<StaffMemberDetailDto> GetStaffMemberAsync(Guid id, CancellationToken ct = default)
        {
            var member = _members.FirstOrDefault(m => m.Id == id);
            if (member == null)
            {
                throw new NotFoundException("StaffMember", id);
            }

            _detailData.TryGetValue(id, out var data);

            var detail = new StaffMemberDetailDto
            {
                Id = member.Id,
                EmployeeNumber = member.EmployeeNumber,
                FirstName = member.FirstName,
                LastName = member.LastName,
                FullName = member.FullName,
                Email = member.Email,
                PhoneNumber = member.PhoneNumber,
                Role = member.Role,
                Department = member.Department,
                IsActive = member.IsActive,
                CreatedAt = member.CreatedAt,
                UpdatedAt = member.UpdatedAt,
                Skills = data.Skills ?? Array.Empty<StaffSkillDto>(),
                UpcomingAllocations = data.Allocations ?? Array.Empty<AllocationSummaryDto>(),
                LeaveBalanceDays = null
            };

            return Task.FromResult(detail);
        }

        public Task<UpdateStaffMemberResponse> UpdateStaffMemberAsync(Guid id, UpdateStaffMemberRequest request, CancellationToken ct = default)
        {
            var member = _members.FirstOrDefault(m => m.Id == id);
            if (member == null)
            {
                throw new NotFoundException("StaffMember", id);
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName)) member.FirstName = request.FirstName;
            if (!string.IsNullOrWhiteSpace(request.LastName)) member.LastName = request.LastName;
            if (request.PhoneNumber != null) member.PhoneNumber = request.PhoneNumber;
            if (request.Department != null) member.Department = request.Department;

            var affected = new List<AllocationSummaryDto>();
            if (request.Role.HasValue && request.Role.Value != member.Role)
            {
                if (_roleChangeAllocations.TryGetValue((id, request.Role.Value), out var allocs))
                {
                    affected.AddRange(allocs);
                }
                member.Role = request.Role.Value;
            }

            return Task.FromResult(new UpdateStaffMemberResponse
            {
                StaffMember = member,
                AffectedAllocations = affected
            });
        }

        public Task DeactivateStaffMemberAsync(Guid id, DeactivateStaffMemberRequest request, CancellationToken ct = default)
        {
            var member = _members.FirstOrDefault(m => m.Id == id);
            if (member == null)
            {
                throw new NotFoundException("StaffMember", id);
            }

            if (_blockedDeactivations.TryGetValue(id, out var allocs) && allocs.Count > 0)
            {
                throw new StaffDeactivationConflictException(allocs, "Cannot deactivate staff member who still holds confirmed future allocations.");
            }

            member.IsActive = false;
            return Task.CompletedTask;
        }

        public Task<StaffMemberDto> ReactivateStaffMemberAsync(Guid id, CancellationToken ct = default)
        {
            var member = _members.FirstOrDefault(m => m.Id == id);
            if (member == null)
            {
                throw new NotFoundException("StaffMember", id);
            }

            if (member.IsActive)
            {
                throw new ConflictException(MessageCode.Conflict, "Staff member is already active.");
            }

            var duplicateEmail = _members.Any(m => m.Id != id && m.Email.Equals(member.Email, StringComparison.OrdinalIgnoreCase) && m.IsActive);
            if (duplicateEmail)
            {
                throw new ConflictException(MessageCode.Conflict, "The email is now in use by an active staff member.");
            }

            member.IsActive = true;
            return Task.FromResult(member);
        }
    }

    #endregion
}
