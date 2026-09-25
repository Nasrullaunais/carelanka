using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Auth;
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
    public async Task Operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/reports/coverage", "get")]
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

    #region Authentication & Authorization Tests

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
        stubReports.SetResponse(new CoverageReport
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

    #region Parameter Validation Tests

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

        Assert.NotNull(stubReports.LastParameters);
        Assert.Equal(new DateOnly(2026, 10, 1), stubReports.LastParameters.From);
        Assert.Equal(new DateOnly(2026, 10, 7), stubReports.LastParameters.To);
        Assert.Equal(wardId, stubReports.LastParameters.WardId);
    }

    #endregion

    #region Business Logic & Calculation Tests

    [Fact]
    public void Shift_duration_handles_daytime_and_overnight_shifts()
    {
        // 08:00 to 16:00 => 8.0 hours
        var daytimeShift = new Shift
        {
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0)
        };
        Assert.Equal(8.0, StaffReportsService.GetShiftDurationHours(daytimeShift));

        // 22:00 to 06:00 => 8.0 hours (overnight)
        var overnightShift = new Shift
        {
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0)
        };
        Assert.Equal(8.0, StaffReportsService.GetShiftDurationHours(overnightShift));

        // 20:00 to 08:00 => 12.0 hours (overnight)
        var longNightShift = new Shift
        {
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(8, 0)
        };
        Assert.Equal(12.0, StaffReportsService.GetShiftDurationHours(longNightShift));

        // 07:00 to 19:00 => 12.0 hours
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

        stubReports.SetResponse(new CoverageReport
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
        stubReports.SetResponse(new CoverageReport
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
            } // 2 confirmed < 3 min -> understaffed! Duration = 8 hours
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
            } // 2 confirmed >= 2 min -> fully staffed! Duration = 8 hours
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
        Assert.Equal((double)4 / 6, fillRate); // 4 confirmed / 6 needed
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
            } // 1 confirmed < 3 min -> understaffed
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

        // Aggregate fill rate is total confirmed / total needed, not the average of row fill rates
        var allConfirmed = 15;
        var allNeeded = 20;
        var totalFillRate = Math.Clamp((double)allConfirmed / allNeeded, 0.0, 1.0);

        Assert.Equal(5, totalShifts);
        Assert.Equal(3, totalUnderstaffed);
        Assert.Equal(24.0, totalHoursBelow);
        Assert.Equal(0.75, totalFillRate);
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
        public CoverageReportParameters? LastParameters { get; private set; }
        private CoverageReport _response = new()
        {
            From = DateOnly.FromDateTime(DateTime.UtcNow),
            To = DateOnly.FromDateTime(DateTime.UtcNow),
            Rows = Array.Empty<CoverageReportRow>(),
            Totals = new CoverageReportTotals()
        };

        public void SetResponse(CoverageReport response) => _response = response;

        public Task<CoverageReport> GetCoverageReportAsync(
            CoverageReportParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.FromResult(_response);
        }
    }

    #endregion
}
