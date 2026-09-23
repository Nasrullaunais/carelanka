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

public sealed class SkillEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/skills", "get", "listSkills")]
    [InlineData("/skills", "post", "createSkill")]
    [InlineData("/skills/{id}", "put", "updateSkill")]
    [InlineData("/skills/{id}", "delete", "retireSkill")]
    [InlineData("/staff/{id}/skills", "get", "listStaffSkills")]
    [InlineData("/staff/{id}/skills", "post", "grantStaffSkill")]
    [InlineData("/staff/{staffId}/skills/{skillId}", "delete", "revokeStaffSkill")]
    public async Task Skill_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/skills", "get")]
    [InlineData("/skills", "post")]
    [InlineData("/skills/{id}", "put")]
    [InlineData("/skills/{id}", "delete")]
    [InlineData("/staff/{id}/skills", "get")]
    [InlineData("/staff/{id}/skills", "post")]
    [InlineData("/staff/{staffId}/skills/{skillId}", "delete")]
    public async Task Skill_response_statuses_match_contract(string path, string method)
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
    public async Task Skills_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new SkillTestApplication();
        using var client = app.CreateClient();

        var getSkills = await client.GetAsync("/api/skills");
        var postSkill = await client.PostAsJsonAsync("/api/skills", new { name = "ICU" });
        var putSkill = await client.PutAsJsonAsync($"/api/skills/{Guid.NewGuid()}", new { name = "ICU" });
        var deleteSkill = await client.DeleteAsync($"/api/skills/{Guid.NewGuid()}");
        var getStaffSkills = await client.GetAsync($"/api/staff/{Guid.NewGuid()}/skills");
        var postStaffSkill = await client.PostAsJsonAsync($"/api/staff/{Guid.NewGuid()}/skills", new { skill_id = Guid.NewGuid() });
        var deleteStaffSkill = await client.DeleteAsync($"/api/staff/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, getSkills.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getStaffSkills.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postStaffSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteStaffSkill.StatusCode);
    }

    [Fact]
    public async Task Skills_endpoints_reject_patient_role_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new SkillTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("patient", "patient"));

        var getSkills = await client.GetAsync("/api/skills");
        var postSkill = await client.PostAsJsonAsync("/api/skills", new { name = "ICU" });
        var putSkill = await client.PutAsJsonAsync($"/api/skills/{Guid.NewGuid()}", new { name = "ICU" });
        var deleteSkill = await client.DeleteAsync($"/api/skills/{Guid.NewGuid()}");
        var getStaffSkills = await client.GetAsync($"/api/staff/{Guid.NewGuid()}/skills");
        var postStaffSkill = await client.PostAsJsonAsync($"/api/staff/{Guid.NewGuid()}/skills", new { skill_id = Guid.NewGuid() });
        var deleteStaffSkill = await client.DeleteAsync($"/api/staff/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, getSkills.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getStaffSkills.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postStaffSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteStaffSkill.StatusCode);
    }

    [Fact]
    public async Task Non_admin_staff_cannot_create_update_or_retire_skills()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new SkillTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("ward_nurse", "staff"));

        var postSkill = await client.PostAsJsonAsync("/api/skills", new { name = "ICU" });
        var putSkill = await client.PutAsJsonAsync($"/api/skills/{Guid.NewGuid()}", new { name = "ICU" });
        var deleteSkill = await client.DeleteAsync($"/api/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, postSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteSkill.StatusCode);
    }

    [Fact]
    public async Task Non_admin_staff_cannot_grant_or_revoke_staff_skills()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new SkillTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("duty_manager", "staff"));

        var postStaffSkill = await client.PostAsJsonAsync($"/api/staff/{Guid.NewGuid()}/skills", new { skill_id = Guid.NewGuid() });
        var deleteStaffSkill = await client.DeleteAsync($"/api/staff/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, postStaffSkill.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteStaffSkill.StatusCode);
    }

    [Fact]
    public async Task Any_staff_role_can_list_skills()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = Guid.NewGuid(), Name = "ICU Certified", StaffCount = 3 });

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.GetAsync("/api/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<SkillDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("ICU Certified", list[0].Name);
    }

    #endregion

    #region Decision 2: Self-Service Authorization for GET /api/staff/{id}/skills

    [Fact]
    public async Task HospitalAdministrator_can_list_any_staff_skills()
    {
        using var environment = TestEnvironment.Use();
        var staffId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.StaffSkills[staffId] = new List<StaffSkillDto>
        {
            new() { SkillId = Guid.NewGuid(), SkillName = "ICU", IsValid = true, GrantedAt = DateTimeOffset.UtcNow }
        };

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/staff/{staffId}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var skills = await response.Content.ReadFromJsonAsync<List<StaffSkillDto>>(JsonOptions);
        Assert.NotNull(skills);
        Assert.Single(skills);
    }

    [Fact]
    public async Task DutyManager_can_list_any_staff_skills()
    {
        using var environment = TestEnvironment.Use();
        var staffId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.StaffSkills[staffId] = new List<StaffSkillDto>
        {
            new() { SkillId = Guid.NewGuid(), SkillName = "Triage", IsValid = true, GrantedAt = DateTimeOffset.UtcNow }
        };

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/staff/{staffId}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var skills = await response.Content.ReadFromJsonAsync<List<StaffSkillDto>>(JsonOptions);
        Assert.NotNull(skills);
        Assert.Single(skills);
    }

    [Fact]
    public async Task Staff_member_can_list_their_own_skills()
    {
        using var environment = TestEnvironment.Use();
        var myStaffId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.StaffSkills[myStaffId] = new List<StaffSkillDto>
        {
            new() { SkillId = Guid.NewGuid(), SkillName = "BLS", IsValid = true, GrantedAt = DateTimeOffset.UtcNow }
        };

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", subject: myStaffId));

        var response = await client.GetAsync($"/api/staff/{myStaffId}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var skills = await response.Content.ReadFromJsonAsync<List<StaffSkillDto>>(JsonOptions);
        Assert.NotNull(skills);
        Assert.Single(skills);
    }

    [Fact]
    public async Task Staff_member_cannot_list_another_staff_members_skills()
    {
        using var environment = TestEnvironment.Use();
        var myStaffId = Guid.NewGuid();
        var otherStaffId = Guid.NewGuid();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", subject: myStaffId));

        var response = await client.GetAsync($"/api/staff/{otherStaffId}/skills");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Validation & CRUD Behavior Tests

    [Fact]
    public async Task Create_skill_rejects_empty_name()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new SkillTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/skills", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_skill_succeeds_for_administrator()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();
        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/skills", new
        {
            name = "Pediatric ACLS",
            description = "Advanced cardiac life support for pediatrics"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SkillDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Pediatric ACLS", created.Name);
        Assert.Equal(0, created.StaffCount);
    }

    [Fact]
    public async Task Update_skill_succeeds_for_administrator()
    {
        using var environment = TestEnvironment.Use();
        var skillId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = skillId, Name = "Old Name" });

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PutAsJsonAsync($"/api/skills/{skillId}", new
        {
            name = "New Name",
            description = "Updated description"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SkillDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
    }

    [Fact]
    public async Task Retire_skill_returns_204_for_administrator()
    {
        using var environment = TestEnvironment.Use();
        var skillId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = skillId, Name = "Skill to Retire" });

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/skills/{skillId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Grant_staff_skill_succeeds_and_revoke_returns_affected_allocations()
    {
        using var environment = TestEnvironment.Use();
        var staffId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // Grant
        var grantResponse = await client.PostAsJsonAsync($"/api/staff/{staffId}/skills", new
        {
            skill_id = skillId,
            valid_from = "2026-01-01",
            expires_at = "2027-01-01"
        });
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);
        var granted = await grantResponse.Content.ReadFromJsonAsync<StaffSkillDto>(JsonOptions);
        Assert.NotNull(granted);
        Assert.Equal(skillId, granted.SkillId);

        // Revoke
        var revokeResponse = await client.DeleteAsync($"/api/staff/{staffId}/skills/{skillId}");
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var revoked = await revokeResponse.Content.ReadFromJsonAsync<RevokeStaffSkillResponse>(JsonOptions);
        Assert.NotNull(revoked);
        Assert.NotNull(revoked.AffectedAllocations);
    }

    [Fact]
    public async Task Search_skills_filters_by_name()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = Guid.NewGuid(), Name = "ICU Certified" });
        stub.Skills.Add(new SkillDto { Id = Guid.NewGuid(), Name = "Pediatric Care" });

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.GetAsync("/api/skills?search=icu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<SkillDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("ICU Certified", list[0].Name);
    }

    [Fact]
    public async Task Create_skill_rejects_duplicate_name_with_409()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = Guid.NewGuid(), Name = "ICU Certified" });

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/skills", new { name = "ICU Certified" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_skill_returns_404_when_skill_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PutAsJsonAsync($"/api/skills/{Guid.NewGuid()}", new { name = "New Name" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Retire_skill_returns_404_when_skill_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Retire_skill_rejects_with_409_when_shift_requires_it()
    {
        using var environment = TestEnvironment.Use();
        var skillId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.Skills.Add(new SkillDto { Id = skillId, Name = "Required Skill" });
        stub.BlockedRetireSkillIds.Add(skillId);

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/skills/{skillId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Grant_staff_skill_rejects_invalid_date_range_with_400()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/staff/{Guid.NewGuid()}/skills", new
        {
            skill_id = Guid.NewGuid(),
            valid_from = "2027-01-01",
            expires_at = "2026-01-01"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grant_staff_skill_rejects_duplicate_with_409()
    {
        using var environment = TestEnvironment.Use();
        var staffId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var stub = new StubSkillService();
        stub.StaffSkills[staffId] = new List<StaffSkillDto>
        {
            new() { SkillId = skillId, SkillName = "Existing", IsValid = true, GrantedAt = DateTimeOffset.UtcNow }
        };

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/staff/{staffId}/skills", new
        {
            skill_id = skillId
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_staff_skill_returns_404_when_assignment_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubSkillService();

        await using var app = new SkillTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/staff/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private sealed class SkillTestApplication : WebApplicationFactory<Program>
    {
        private readonly ISkillService? _stub;

        public SkillTestApplication(ISkillService? stub = null)
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
                    services.RemoveAll<ISkillService>();
                    services.AddSingleton(_stub);
                }
            });
        }
    }

    private sealed class StubSkillService : ISkillService
    {
        public List<SkillDto> Skills { get; } = new();
        public HashSet<Guid> BlockedRetireSkillIds { get; } = new();
        public Dictionary<Guid, List<StaffSkillDto>> StaffSkills { get; } = new();

        public Task<IReadOnlyList<SkillDto>> ListSkillsAsync(string? search, CancellationToken ct = default)
        {
            IEnumerable<SkillDto> query = Skills;
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult<IReadOnlyList<SkillDto>>(query.ToList());
        }

        public Task<SkillDto> CreateSkillAsync(CreateSkillRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException(MessageCode.ValidationFailed);
            }
            if (Skills.Any(s => s.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(MessageCode.Conflict);
            }
            var created = new SkillDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                StaffCount = 0
            };
            Skills.Add(created);
            return Task.FromResult(created);
        }

        public Task<SkillDto> UpdateSkillAsync(Guid id, UpdateSkillRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException(MessageCode.ValidationFailed);
            }
            var skill = Skills.FirstOrDefault(s => s.Id == id);
            if (skill is null)
            {
                throw new NotFoundException("Skill", id);
            }
            if (Skills.Any(s => s.Id != id && s.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(MessageCode.Conflict);
            }
            skill.Name = request.Name;
            skill.Description = request.Description;
            return Task.FromResult(skill);
        }

        public Task RetireSkillAsync(Guid id, CancellationToken ct = default)
        {
            var skill = Skills.FirstOrDefault(s => s.Id == id);
            if (skill is null)
            {
                throw new NotFoundException("Skill", id);
            }
            if (BlockedRetireSkillIds.Contains(id))
            {
                throw new ConflictException(MessageCode.Conflict);
            }
            Skills.Remove(skill);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StaffSkillDto>> ListStaffSkillsAsync(Guid staffMemberId, CancellationToken ct = default)
        {
            StaffSkills.TryGetValue(staffMemberId, out var list);
            return Task.FromResult<IReadOnlyList<StaffSkillDto>>(list ?? new List<StaffSkillDto>());
        }

        public Task<StaffSkillDto> GrantStaffSkillAsync(Guid staffMemberId, GrantStaffSkillRequest request, CancellationToken ct = default)
        {
            if (request.ValidFrom.HasValue && request.ExpiresAt.HasValue && request.ValidFrom.Value > request.ExpiresAt.Value)
            {
                throw new BadRequestException(MessageCode.ValidationFailed);
            }

            if (StaffSkills.TryGetValue(staffMemberId, out var existing) && existing.Any(s => s.SkillId == request.SkillId))
            {
                throw new ConflictException(MessageCode.Conflict);
            }

            var item = new StaffSkillDto
            {
                SkillId = request.SkillId,
                SkillName = "Skill " + request.SkillId,
                ValidFrom = request.ValidFrom,
                ExpiresAt = request.ExpiresAt,
                IsValid = true,
                GrantedAt = DateTimeOffset.UtcNow
            };

            if (existing is null)
            {
                existing = new List<StaffSkillDto>();
                StaffSkills[staffMemberId] = existing;
            }
            existing.Add(item);
            return Task.FromResult(item);
        }

        public Task<RevokeStaffSkillResponse> RevokeStaffSkillAsync(Guid staffMemberId, Guid skillId, CancellationToken ct = default)
        {
            if (!StaffSkills.TryGetValue(staffMemberId, out var list) || !list.Any(s => s.SkillId == skillId))
            {
                throw new NotFoundException("StaffMemberSkill", skillId);
            }
            list.RemoveAll(s => s.SkillId == skillId);
            return Task.FromResult(new RevokeStaffSkillResponse
            {
                AffectedAllocations = Array.Empty<AllocationSummaryDto>()
            });
        }
    }

    #endregion
}
