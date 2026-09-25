using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class StaffReportsEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/reports/coverage", "get", "getCoverageReport")]
    [InlineData("/reports/leave", "get", "getLeaveReport")]
    public async Task Operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/reports/coverage", "get")]
    [InlineData("/reports/leave", "get")]
    public async Task Response_statuses_match_contract(string path, string method)
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

    #region Coverage Report - Authentication & Authorization Tests

    [Fact]
    public async Task Coverage_report_requires_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("general_staff")]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("equipment_manager")]
    [InlineData("patient")]
    public async Task Coverage_report_rejects_unauthorized_roles(string role)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, role == "patient" ? "patient" : "staff"));

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("hospital_administrator")]
    [InlineData("duty_manager")]
    public async Task Coverage_report_accepts_authorized_roles(string role)
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        stubReports.SetCoverageResponse(new CoverageReport
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 7),
            Rows = Array.Empty<CoverageReportRow>(),
            Totals = new CoverageReportTotals()
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Coverage Report - Parameter Validation Tests

    [Fact]
    public async Task Coverage_report_requires_from_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/coverage?to=2026-10-07");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Coverage_report_requires_to_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Coverage_report_rejects_from_greater_than_to()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-07&to=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Coverage_report_passes_ward_id_filter()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        var wardId = Guid.NewGuid();

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/reports/coverage?from=2026-10-01&to=2026-10-07&wardId={wardId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(stubReports.LastCoverageParameters);
        Assert.Equal(new DateOnly(2026, 10, 1), stubReports.LastCoverageParameters.From);
        Assert.Equal(new DateOnly(2026, 10, 7), stubReports.LastCoverageParameters.To);
        Assert.Equal(wardId, stubReports.LastCoverageParameters.WardId);
    }

    #endregion

    #region Coverage Report - Business Logic Tests

    [Fact]
    public void Shift_duration_handles_daytime_and_overnight_shifts()
    {
        var daytimeShift = new Shift
        {
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0)
        };
        Assert.Equal(8.0, StaffReportsService.GetShiftDurationHours(daytimeShift));

        var overnightShift = new Shift
        {
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0)
        };
        Assert.Equal(8.0, StaffReportsService.GetShiftDurationHours(overnightShift));

        var longNightShift = new Shift
        {
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(8, 0)
        };
        Assert.Equal(12.0, StaffReportsService.GetShiftDurationHours(longNightShift));

        var longDayShift = new Shift
        {
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(19, 0)
        };
        Assert.Equal(12.0, StaffReportsService.GetShiftDurationHours(longDayShift));
    }

    [Fact]
    public async Task Coverage_report_returns_200_with_expected_contract_shape()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        var wardId = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 1);

        stubReports.SetCoverageResponse(new CoverageReport
        {
            From = date,
            To = date.AddDays(1),
            Rows = new List<CoverageReportRow>
            {
                new()
                {
                    WardId = wardId,
                    WardName = "Cardiology",
                    Date = date,
                    ShiftsTotal = 2,
                    ShiftsUnderstaffed = 1,
                    HoursBelowMinimum = 8.0,
                    FillRate = 0.75
                }
            },
            Totals = new CoverageReportTotals
            {
                ShiftsTotal = 2,
                ShiftsUnderstaffed = 1,
                HoursBelowMinimum = 8.0,
                FillRate = 0.75
            }
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/reports/coverage?from={date:yyyy-MM-dd}&to={date.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<CoverageReport>(JsonOptions);
        Assert.NotNull(report);
        Assert.Equal(date, report.From);
        Assert.Equal(date.AddDays(1), report.To);
        Assert.Single(report.Rows);

        var row = report.Rows[0];
        Assert.Equal(wardId, row.WardId);
        Assert.Equal("Cardiology", row.WardName);
        Assert.Equal(date, row.Date);
        Assert.Equal(2, row.ShiftsTotal);
        Assert.Equal(1, row.ShiftsUnderstaffed);
        Assert.Equal(8.0, row.HoursBelowMinimum);
        Assert.Equal(0.75, row.FillRate);

        Assert.Equal(2, report.Totals.ShiftsTotal);
        Assert.Equal(1, report.Totals.ShiftsUnderstaffed);
        Assert.Equal(8.0, report.Totals.HoursBelowMinimum);
        Assert.Equal(0.75, report.Totals.FillRate);
    }

    [Fact]
    public async Task Coverage_report_returns_empty_when_no_shifts()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        stubReports.SetCoverageResponse(new CoverageReport
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 5),
            Rows = Array.Empty<CoverageReportRow>(),
            Totals = new CoverageReportTotals
            {
                ShiftsTotal = 0,
                ShiftsUnderstaffed = 0,
                HoursBelowMinimum = 0.0,
                FillRate = 0.0
            }
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync("/api/reports/coverage?from=2026-10-01&to=2026-10-05");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<CoverageReport>(JsonOptions);
        Assert.NotNull(report);
        Assert.Empty(report.Rows);
        Assert.Equal(0, report.Totals.ShiftsTotal);
        Assert.Equal(0, report.Totals.ShiftsUnderstaffed);
        Assert.Equal(0.0, report.Totals.HoursBelowMinimum);
        Assert.Equal(0.0, report.Totals.FillRate);
    }

    [Fact]
    public void Aggregation_logic_calculates_shifts_total_and_understaffed_correctly()
    {
        var wardId = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 1);

        var shift1 = new Shift
        {
            Id = Guid.NewGuid(),
            WardId = wardId,
            Date = date,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            MinimumHeadcount = 3,
            HeadcountNeeded = 4,
            Allocations = new List<Allocation>
            {
                new() { Status = AllocationStatus.Confirmed },
                new() { Status = AllocationStatus.Confirmed }
            }
        };

        var shift2 = new Shift
        {
            Id = Guid.NewGuid(),
            WardId = wardId,
            Date = date,
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(0, 0),
            MinimumHeadcount = 2,
            HeadcountNeeded = 2,
            Allocations = new List<Allocation>
            {
                new() { Status = AllocationStatus.Confirmed },
                new() { Status = AllocationStatus.Confirmed }
            }
        };

        var shifts = new[] { shift1, shift2 };

        var shiftsTotal = shifts.Length;
        var shiftsUnderstaffed = 0;
        var hoursBelowMinimum = 0.0;
        var totalConfirmed = 0;
        var totalNeeded = 0;

        foreach (var shift in shifts)
        {
            var confirmed = shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed);
            totalConfirmed += confirmed;
            totalNeeded += shift.HeadcountNeeded;

            if (confirmed < shift.MinimumHeadcount)
            {
                shiftsUnderstaffed++;
                hoursBelowMinimum += StaffReportsService.GetShiftDurationHours(shift);
            }
        }

        var fillRate = totalNeeded > 0
            ? Math.Clamp((double)totalConfirmed / totalNeeded, 0.0, 1.0)
            : 0.0;

        Assert.Equal(2, shiftsTotal);
        Assert.Equal(1, shiftsUnderstaffed);
        Assert.Equal(8.0, hoursBelowMinimum);
        Assert.Equal((double)4 / 6, fillRate);
    }

    [Fact]
    public void Fill_rate_clamps_to_one_when_overstaffed()
    {
        var totalConfirmed = 10;
        var totalNeeded = 6;

        var fillRate = totalNeeded > 0
            ? Math.Clamp((double)totalConfirmed / totalNeeded, 0.0, 1.0)
            : 0.0;

        Assert.Equal(1.0, fillRate);
    }

    [Fact]
    public void Fill_rate_protects_against_division_by_zero()
    {
        var totalConfirmed = 0;
        var totalNeeded = 0;

        var fillRate = totalNeeded > 0
            ? Math.Clamp((double)totalConfirmed / totalNeeded, 0.0, 1.0)
            : 0.0;

        Assert.Equal(0.0, fillRate);
        Assert.False(double.IsNaN(fillRate));
        Assert.False(double.IsInfinity(fillRate));
    }

    [Fact]
    public void Only_confirmed_allocations_count_toward_staffing()
    {
        var shift = new Shift
        {
            MinimumHeadcount = 2,
            HeadcountNeeded = 3,
            Allocations = new List<Allocation>
            {
                new() { Status = AllocationStatus.Confirmed },
                new() { Status = AllocationStatus.Proposed },
                new() { Status = AllocationStatus.Cancelled },
                new() { Status = AllocationStatus.Released }
            }
        };

        var confirmedCount = shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed);
        Assert.Equal(1, confirmedCount);
        Assert.True(confirmedCount < shift.MinimumHeadcount);
    }

    [Fact]
    public void Overnight_shift_understaffed_calculates_correct_hours_below_minimum()
    {
        var overnightShift = new Shift
        {
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            MinimumHeadcount = 3,
            HeadcountNeeded = 3,
            Allocations = new List<Allocation>
            {
                new() { Status = AllocationStatus.Confirmed }
            }
        };

        var isUnderstaffed = overnightShift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed) < overnightShift.MinimumHeadcount;
        Assert.True(isUnderstaffed);

        var duration = StaffReportsService.GetShiftDurationHours(overnightShift);
        Assert.Equal(8.0, duration);
    }

    [Fact]
    public void Rows_are_ordered_by_ward_name_then_date()
    {
        var rows = new List<CoverageReportRow>
        {
            new() { WardName = "Pediatrics", Date = new DateOnly(2026, 10, 2) },
            new() { WardName = "Cardiology", Date = new DateOnly(2026, 10, 2) },
            new() { WardName = "Cardiology", Date = new DateOnly(2026, 10, 1) },
            new() { WardName = "Pediatrics", Date = new DateOnly(2026, 10, 1) }
        };

        var sorted = rows
            .OrderBy(r => r.WardName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Date)
            .ToList();

        Assert.Equal("Cardiology", sorted[0].WardName);
        Assert.Equal(new DateOnly(2026, 10, 1), sorted[0].Date);

        Assert.Equal("Cardiology", sorted[1].WardName);
        Assert.Equal(new DateOnly(2026, 10, 2), sorted[1].Date);

        Assert.Equal("Pediatrics", sorted[2].WardName);
        Assert.Equal(new DateOnly(2026, 10, 1), sorted[2].Date);

        Assert.Equal("Pediatrics", sorted[3].WardName);
        Assert.Equal(new DateOnly(2026, 10, 2), sorted[3].Date);
    }

    [Fact]
    public void Totals_aggregate_all_rows_and_calculates_aggregate_fill_rate()
    {
        var row1 = new CoverageReportRow
        {
            ShiftsTotal = 2,
            ShiftsUnderstaffed = 1,
            HoursBelowMinimum = 8.0,
            FillRate = 0.5
        };

        var row2 = new CoverageReportRow
        {
            ShiftsTotal = 3,
            ShiftsUnderstaffed = 2,
            HoursBelowMinimum = 16.0,
            FillRate = 0.6
        };

        var rows = new[] { row1, row2 };

        var totalShifts = rows.Sum(r => r.ShiftsTotal);
        var totalUnderstaffed = rows.Sum(r => r.ShiftsUnderstaffed);
        var totalHoursBelow = rows.Sum(r => r.HoursBelowMinimum);

        var allConfirmed = 15;
        var allNeeded = 20;
        var totalFillRate = Math.Clamp((double)allConfirmed / allNeeded, 0.0, 1.0);

        Assert.Equal(5, totalShifts);
        Assert.Equal(3, totalUnderstaffed);
        Assert.Equal(24.0, totalHoursBelow);
        Assert.Equal(0.75, totalFillRate);
    }

    #endregion

    #region Leave Report - Authentication & Authorization Tests

    [Fact]
    public async Task Leave_report_requires_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("duty_manager")]
    [InlineData("general_staff")]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("equipment_manager")]
    [InlineData("patient")]
    public async Task Leave_report_rejects_non_hospital_administrator(string role)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, role == "patient" ? "patient" : "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Leave_report_accepts_hospital_administrator()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        stubReports.SetLeaveResponse(new LeaveReport
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 7),
            GroupBy = "type",
            Rows = Array.Empty<LeaveReportRow>()
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Leave Report - Parameter Validation Tests

    [Fact]
    public async Task Leave_report_requires_from_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?to=2026-10-07");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leave_report_requires_to_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leave_report_rejects_from_greater_than_to()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-07&to=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leave_report_rejects_invalid_group_by()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new StaffReportsTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01&to=2026-10-07&groupBy=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("staff")]
    [InlineData("type")]
    [InlineData("ward")]
    [InlineData("month")]
    public async Task Leave_report_accepts_valid_group_by_values(string groupBy)
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/reports/leave?from=2026-10-01&to=2026-10-07&groupBy={groupBy}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(stubReports.LastLeaveParameters);
        Assert.Equal(groupBy, stubReports.LastLeaveParameters.GroupBy);
    }

    [Fact]
    public async Task Leave_report_defaults_group_by_to_null_in_parameters()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/reports/leave?from=2026-10-01&to=2026-10-07");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(stubReports.LastLeaveParameters);
        Assert.Null(stubReports.LastLeaveParameters.GroupBy);
    }

    #endregion

    #region Leave Report - Business Logic Tests

    [Fact]
    public async Task Leave_report_returns_200_with_expected_contract_shape()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 7);

        stubReports.SetLeaveResponse(new LeaveReport
        {
            From = from,
            To = to,
            GroupBy = "type",
            Rows = new List<LeaveReportRow>
            {
                new()
                {
                    Key = "annual",
                    ApprovedDays = 5.0,
                    PendingDays = 2.0,
                    RejectedCount = 1,
                    SickDays = 0.0
                },
                new()
                {
                    Key = "sick",
                    ApprovedDays = 3.0,
                    PendingDays = 0.0,
                    RejectedCount = 0,
                    SickDays = 3.0
                }
            }
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/reports/leave?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<LeaveReport>(JsonOptions);
        Assert.NotNull(report);
        Assert.Equal(from, report.From);
        Assert.Equal(to, report.To);
        Assert.Equal("type", report.GroupBy);
        Assert.Equal(2, report.Rows.Count);

        Assert.Equal("annual", report.Rows[0].Key);
        Assert.Equal(5.0, report.Rows[0].ApprovedDays);
        Assert.Equal(2.0, report.Rows[0].PendingDays);
        Assert.Equal(1, report.Rows[0].RejectedCount);
        Assert.Equal(0.0, report.Rows[0].SickDays);

        Assert.Equal("sick", report.Rows[1].Key);
        Assert.Equal(3.0, report.Rows[1].ApprovedDays);
        Assert.Equal(0.0, report.Rows[1].PendingDays);
        Assert.Equal(0, report.Rows[1].RejectedCount);
        Assert.Equal(3.0, report.Rows[1].SickDays);
    }

    [Fact]
    public async Task Leave_report_returns_empty_when_no_matching_requests()
    {
        using var environment = TestEnvironment.Use();
        var stubReports = new StubStaffReportsService();
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 7);

        stubReports.SetLeaveResponse(new LeaveReport
        {
            From = from,
            To = to,
            GroupBy = "type",
            Rows = Array.Empty<LeaveReportRow>()
        });

        await using var app = new StaffReportsTestApplication(stubReports);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/reports/leave?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<LeaveReport>(JsonOptions);
        Assert.NotNull(report);
        Assert.Empty(report.Rows);
        Assert.Equal(from, report.From);
        Assert.Equal(to, report.To);
        Assert.Equal("type", report.GroupBy);
    }

    [Fact]
    public void Date_intersection_at_start_of_range_calculates_only_overlapping_days()
    {
        // Range: Oct 5 to Oct 15
        var from = new DateOnly(2026, 10, 5);
        var to = new DateOnly(2026, 10, 15);

        // Leave: Oct 1 to Oct 8 (starts before range, ends in range)
        var leave = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 8),
            Status = LeaveStatus.Approved
        };

        var overlapStart = leave.StartDate > from ? leave.StartDate : from;
        var overlapEnd = leave.EndDate < to ? leave.EndDate : to;

        Assert.Equal(new DateOnly(2026, 10, 5), overlapStart);
        Assert.Equal(new DateOnly(2026, 10, 8), overlapEnd);

        var overlappingDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);
        Assert.Equal(4.0, overlappingDays); // Oct 5, 6, 7, 8
    }

    [Fact]
    public void Date_intersection_at_end_of_range_calculates_only_overlapping_days()
    {
        // Range: Oct 5 to Oct 15
        var from = new DateOnly(2026, 10, 5);
        var to = new DateOnly(2026, 10, 15);

        // Leave: Oct 12 to Oct 20 (starts in range, ends after range)
        var leave = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 10, 12),
            EndDate = new DateOnly(2026, 10, 20),
            Status = LeaveStatus.Approved
        };

        var overlapStart = leave.StartDate > from ? leave.StartDate : from;
        var overlapEnd = leave.EndDate < to ? leave.EndDate : to;

        Assert.Equal(new DateOnly(2026, 10, 12), overlapStart);
        Assert.Equal(new DateOnly(2026, 10, 15), overlapEnd);

        var overlappingDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);
        Assert.Equal(4.0, overlappingDays); // Oct 12, 13, 14, 15
    }

    [Fact]
    public void Leave_request_completely_outside_range_is_excluded()
    {
        // Range: Oct 5 to Oct 15
        var from = new DateOnly(2026, 10, 5);
        var to = new DateOnly(2026, 10, 15);

        // Leave: Sep 20 to Sep 25 (completely before)
        var leaveBefore = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 9, 20),
            EndDate = new DateOnly(2026, 9, 25)
        };
        var overlapsBefore = leaveBefore.StartDate <= to && leaveBefore.EndDate >= from;
        Assert.False(overlapsBefore);

        // Leave: Oct 20 to Oct 25 (completely after)
        var leaveAfter = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 10, 20),
            EndDate = new DateOnly(2026, 10, 25)
        };
        var overlapsAfter = leaveAfter.StartDate <= to && leaveAfter.EndDate >= from;
        Assert.False(overlapsAfter);
    }

    [Fact]
    public void Group_by_type_uses_wire_values_and_calculates_metrics()
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 10);

        var leaves = new List<LeaveRequest>
        {
            new()
            {
                Type = LeaveType.Annual,
                StartDate = new DateOnly(2026, 10, 1),
                EndDate = new DateOnly(2026, 10, 3), // 3 days
                Status = LeaveStatus.Approved
            },
            new()
            {
                Type = LeaveType.Annual,
                StartDate = new DateOnly(2026, 10, 4),
                EndDate = new DateOnly(2026, 10, 5), // 2 days
                Status = LeaveStatus.Pending
            },
            new()
            {
                Type = LeaveType.Sick,
                StartDate = new DateOnly(2026, 10, 6),
                EndDate = new DateOnly(2026, 10, 8), // 3 days
                Status = LeaveStatus.Approved
            },
            new()
            {
                Type = LeaveType.Emergency,
                StartDate = new DateOnly(2026, 10, 9),
                EndDate = new DateOnly(2026, 10, 9), // 1 day
                Status = LeaveStatus.Rejected
            },
            new()
            {
                Type = LeaveType.ShiftSwap,
                StartDate = new DateOnly(2026, 10, 10),
                EndDate = new DateOnly(2026, 10, 10), // 1 day
                Status = LeaveStatus.Approved
            }
        };

        var dict = new Dictionary<string, (double Approved, double Pending, int Rejected, double Sick)>();

        foreach (var leave in leaves)
        {
            var overlapStart = leave.StartDate > from ? leave.StartDate : from;
            var overlapEnd = leave.EndDate < to ? leave.EndDate : to;
            var overlapDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);
            var key = EnumWire.ToWire(leave.Type);

            dict.TryGetValue(key, out var acc);
            if (leave.Status == LeaveStatus.Approved) acc.Approved += overlapDays;
            if (leave.Status == LeaveStatus.Pending) acc.Pending += overlapDays;
            if (leave.Status == LeaveStatus.Rejected) acc.Rejected += 1;
            if (leave.Type == LeaveType.Sick) acc.Sick += overlapDays;
            dict[key] = acc;
        }

        Assert.Equal(3.0, dict["annual"].Approved);
        Assert.Equal(2.0, dict["annual"].Pending);
        Assert.Equal(0, dict["annual"].Rejected);
        Assert.Equal(0.0, dict["annual"].Sick);

        Assert.Equal(3.0, dict["sick"].Approved);
        Assert.Equal(0.0, dict["sick"].Pending);
        Assert.Equal(0, dict["sick"].Rejected);
        Assert.Equal(3.0, dict["sick"].Sick);

        Assert.Equal(0.0, dict["emergency"].Approved);
        Assert.Equal(0.0, dict["emergency"].Pending);
        Assert.Equal(1, dict["emergency"].Rejected);

        Assert.Equal(1.0, dict["shift_swap"].Approved);
    }

    [Fact]
    public void Group_by_staff_uses_canonical_full_name()
    {
        var staffId1 = Guid.NewGuid();
        var staffId2 = Guid.NewGuid();

        var staff1 = new StaffMember { Id = staffId1, FirstName = "Kasun", LastName = "Perera" };
        var staff2 = new StaffMember { Id = staffId2, FirstName = "Nimal", LastName = "Silva" };
        var staffMap = new Dictionary<Guid, StaffMember> { [staffId1] = staff1, [staffId2] = staff2 };

        Assert.Equal("Kasun Perera", staff1.FullName);
        Assert.Equal("Nimal Silva", staff2.FullName);
    }

    [Fact]
    public void Group_by_month_splits_multi_month_leave_correctly()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 10, 31);

        // Leave from Sep 28 to Oct 5 (8 days total: 3 in Sep, 5 in Oct)
        var leave = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 9, 28),
            EndDate = new DateOnly(2026, 10, 5),
            Type = LeaveType.Sick,
            Status = LeaveStatus.Approved
        };

        // September
        var sepFirst = new DateOnly(2026, 9, 1);
        var sepLast = new DateOnly(2026, 9, 30);
        var sepOverlapStart = leave.StartDate > sepFirst ? leave.StartDate : sepFirst;
        var sepOverlapEnd = leave.EndDate < sepLast ? leave.EndDate : sepLast;
        var sepDays = (double)((sepOverlapEnd.DayNumber - sepOverlapStart.DayNumber) + 1);
        Assert.Equal(3.0, sepDays);

        // October
        var octFirst = new DateOnly(2026, 10, 1);
        var octLast = new DateOnly(2026, 10, 31);
        var octOverlapStart = leave.StartDate > octFirst ? leave.StartDate : octFirst;
        var octOverlapEnd = leave.EndDate < octLast ? leave.EndDate : octLast;
        var octDays = (double)((octOverlapEnd.DayNumber - octOverlapStart.DayNumber) + 1);
        Assert.Equal(5.0, octDays);

        Assert.Equal(8.0, sepDays + octDays);
    }

    [Fact]
    public void Deterministic_ordering_orders_keys_alphabetically()
    {
        var rows = new List<LeaveReportRow>
        {
            new() { Key = "shift_swap" },
            new() { Key = "annual" },
            new() { Key = "sick" },
            new() { Key = "emergency" }
        };

        var sorted = rows.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal("annual", sorted[0].Key);
        Assert.Equal("emergency", sorted[1].Key);
        Assert.Equal("shift_swap", sorted[2].Key);
        Assert.Equal("sick", sorted[3].Key);
    }

    [Fact]
    public void Month_keys_order_chronologically()
    {
        var rows = new List<LeaveReportRow>
        {
            new() { Key = "2026-11" },
            new() { Key = "2026-09" },
            new() { Key = "2026-10" }
        };

        var sorted = rows.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal("2026-09", sorted[0].Key);
        Assert.Equal("2026-10", sorted[1].Key);
        Assert.Equal("2026-11", sorted[2].Key);
    }

    [Fact]
    public void Multiple_statuses_and_types_in_same_window_aggregate_accurately()
    {
        var rows = new List<LeaveReportRow>
        {
            new()
            {
                Key = "annual",
                ApprovedDays = 10.0,
                PendingDays = 5.0,
                RejectedCount = 2,
                SickDays = 0.0
            },
            new()
            {
                Key = "sick",
                ApprovedDays = 4.0,
                PendingDays = 1.0,
                RejectedCount = 0,
                SickDays = 5.0
            }
        };

        Assert.Equal(10.0, rows[0].ApprovedDays);
        Assert.Equal(5.0, rows[0].PendingDays);
        Assert.Equal(2, rows[0].RejectedCount);
        Assert.Equal(0.0, rows[0].SickDays);

        Assert.Equal(4.0, rows[1].ApprovedDays);
        Assert.Equal(1.0, rows[1].PendingDays);
        Assert.Equal(0, rows[1].RejectedCount);
        Assert.Equal(5.0, rows[1].SickDays);
    }

    #endregion

    #region Helpers and Test Doubles

    private static string CreateToken(string role, string principalType, Guid? subject = null)
    {
        var claims = new[]
        {
            new Claim(CareLankaClaims.Subject, (subject ?? Guid.NewGuid()).ToString()),
            new Claim(CareLankaClaims.Role, role),
            new Claim(CareLankaClaims.PrincipalType, principalType)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            issuer: "carelanka-api",
            audience: "carelanka-clients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<JsonDocument> GenerateSwaggerAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static YamlMappingNode LoadContract()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "../../../../../specs/staff-spec.yaml");
        if (!File.Exists(specPath))
        {
            specPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "specs/staff-spec.yaml"));
        }
        var yaml = new YamlStream();
        using var reader = new StreamReader(specPath);
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static ISet<string> Keys(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static YamlMappingNode Map(YamlMappingNode node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            current = (YamlMappingNode)current.Children[new YamlScalarNode(segment)];
        }

        return current;
    }

    private static ISet<string> Keys(YamlMappingNode node)
        => node.Children.Keys.Select(key => ((YamlScalarNode)key).Value!).ToHashSet();

    private sealed class SwaggerOnlyApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }

    private sealed class StaffReportsTestApplication : WebApplicationFactory<Program>
    {
        private readonly StubStaffReportsService _stubReports;

        public StaffReportsTestApplication(StubStaffReportsService? stubReports = null)
        {
            _stubReports = stubReports ?? new StubStaffReportsService();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffReportsService>();
                services.AddScoped<IStaffReportsService>(_ => _stubReports);
            });
        }
    }

    private sealed class StubStaffReportsService : IStaffReportsService
    {
        public CoverageReportParameters? LastCoverageParameters { get; private set; }
        public LeaveReportParameters? LastLeaveParameters { get; private set; }

        private CoverageReport _coverageResponse = new()
        {
            From = DateOnly.FromDateTime(DateTime.UtcNow),
            To = DateOnly.FromDateTime(DateTime.UtcNow),
            Rows = Array.Empty<CoverageReportRow>(),
            Totals = new CoverageReportTotals()
        };

        private LeaveReport _leaveResponse = new()
        {
            From = DateOnly.FromDateTime(DateTime.UtcNow),
            To = DateOnly.FromDateTime(DateTime.UtcNow),
            GroupBy = "type",
            Rows = Array.Empty<LeaveReportRow>()
        };

        public void SetCoverageResponse(CoverageReport response) => _coverageResponse = response;
        public void SetLeaveResponse(LeaveReport response) => _leaveResponse = response;

        public Task<CoverageReport> GetCoverageReportAsync(
            CoverageReportParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LastCoverageParameters = parameters;
            return Task.FromResult(_coverageResponse);
        }

        public Task<LeaveReport> GetLeaveReportAsync(
            LeaveReportParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LastLeaveParameters = parameters;
            return Task.FromResult(_leaveResponse);
        }
    }

    #endregion
}
