using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WardEndpointTests
{
    private readonly ApiApplication _application;

    public WardEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Administrator_can_create_a_ward_and_it_comes_back_on_the_list()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = NewWardName();

        var created = await CreateWardAsync(client, name, "icu", "mixed");
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(name, body.RootElement.GetProperty("name").GetString());
        Assert.Equal("icu", body.RootElement.GetProperty("ward_type").GetString());
        Assert.Equal("mixed", body.RootElement.GetProperty("gender_policy").GetString());
        Assert.True(body.RootElement.GetProperty("is_active").GetBoolean());

        var listed = await client.GetAsync("/api/wards");
        using var wards = await ReadJsonAsync(listed);

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(
            wards.RootElement.EnumerateArray(),
            w => w.GetProperty("name").GetString() == name);
    }

    // hdu is the one wire value that is not the C# member name lowercased, so it is the one
    // that silently becomes high_dependency if the enum is ever renamed for readability.
    [Theory]
    [InlineData("icu")]
    [InlineData("hdu")]
    [InlineData("general")]
    [InlineData("maternity")]
    [InlineData("pediatric")]
    [InlineData("isolation")]
    public async Task Every_published_ward_type_round_trips_on_its_wire_value(string wardType)
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var created = await CreateWardAsync(client, NewWardName(), wardType, "female");
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(wardType, body.RootElement.GetProperty("ward_type").GetString());
    }

    [Fact]
    public async Task A_second_ward_with_the_same_name_is_a_409_carrying_the_patient_message_code()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = NewWardName();

        await CreateWardAsync(client, name, "general", "male");
        var duplicate = await CreateWardAsync(client, name, "general", "male");
        using var body = await ReadJsonAsync(duplicate);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_pat_001", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_retired_ward_frees_its_name_and_only_shows_under_isActive_false()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = NewWardName();

        await CreateWardAsync(client, name, "general", "male", isActive: false);
        var reused = await CreateWardAsync(client, name, "general", "male");

        // The unique index is scoped WHERE is_active, so this is allowed on purpose.
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);

        using var active = await ReadJsonAsync(await client.GetAsync("/api/wards?isActive=true"));
        using var retired = await ReadJsonAsync(await client.GetAsync("/api/wards?isActive=false"));

        Assert.Equal(1, CountNamed(active, name));
        Assert.Equal(1, CountNamed(retired, name));
    }

    [Fact]
    public async Task Filtering_by_ward_type_excludes_the_other_types()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var maternity = NewWardName();
        var general = NewWardName();

        await CreateWardAsync(client, maternity, "maternity", "female");
        await CreateWardAsync(client, general, "general", "male");

        using var body = await ReadJsonAsync(await client.GetAsync("/api/wards?wardType=maternity"));

        Assert.Equal(1, CountNamed(body, maternity));
        Assert.Equal(0, CountNamed(body, general));
    }

    [Fact]
    public async Task A_ward_with_no_beds_reports_zero_rather_than_a_made_up_number()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var created = await CreateWardAsync(client, NewWardName(), "general", "male");
        using var body = await ReadJsonAsync(created);

        // Equipment's register leaves a ward with no beds out of its result entirely, so this
        // is the case where "absent" has to become 0 rather than a missing property.
        Assert.Equal(0, body.RootElement.GetProperty("total_beds").GetInt32());
    }

    [Fact]
    public async Task Total_beds_counts_what_equipment_actually_registered_and_is_never_stored()
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);

        var name = NewWardName();
        using var ward = await ReadJsonAsync(await CreateWardAsync(administrator, name, "icu", "mixed"));
        var wardId = ward.RootElement.GetProperty("id").GetString()!;

        // Beds are Equipment Management's table. We only ever read the count back.
        for (var number = 1; number <= 3; number++)
        {
            var bed = await equipment.PostAsJsonAsync("/api/beds", new
            {
                ward_id = wardId,
                bed_number = $"B{number}",
                has_isolation = false
            });

            Assert.Equal(HttpStatusCode.Created, bed.StatusCode);
        }

        using var listed = await ReadJsonAsync(await administrator.GetAsync("/api/wards"));
        var counted = listed.RootElement.EnumerateArray()
            .Single(w => w.GetProperty("name").GetString() == name)
            .GetProperty("total_beds").GetInt32();

        // Counted on every read, never stored on the ward row: two sources of truth would drift
        // the moment Equipment retires a bed.
        Assert.Equal(3, counted);
    }

    [Fact]
    public async Task Creating_a_ward_is_403_for_a_nurse_and_401_without_a_token()
    {
        using var anonymous = _application.CreateClient();
        var withoutToken = await CreateWardAsync(anonymous, NewWardName(), "general", "male");

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var wrongRole = await CreateWardAsync(nurse, NewWardName(), "general", "male");

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task Any_staff_role_may_read_the_ward_list_because_three_components_depend_on_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var listed = await nurse.GetAsync("/api/wards");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    [Fact]
    public async Task An_unknown_ward_type_is_a_400_not_a_silently_ignored_filter()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var response = await CreateWardAsync(client, NewWardName(), "intensive_care", "male");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // A missing enum is an error, not a default. [Required] on a plain C# enum always passes,
    // because the model binder has already turned an absent key into the first declared member.
    // Every other test in this class sends a full body, which is why neither of these was caught.

    [Fact]
    public async Task A_ward_with_no_type_is_refused_rather_than_created_as_an_icu()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var created = await client.PostAsJsonAsync(
            "/api/wards", new { name = NewWardName(), gender_policy = "mixed" });

        // icu is declared first, so the old default built the most expensive kind of ward in the
        // hospital out of a typo - and Ward's schema is frozen and depended on by three other
        // components, so a wrong ward_type is not a local mistake.
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal("application/problem+json", created.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_ward_with_no_gender_policy_is_refused_rather_than_created_male_only()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var created = await client.PostAsJsonAsync(
            "/api/wards", new { name = NewWardName(), ward_type = "general" });

        // male is declared first, so the old default quietly halved the ward's usable beds: the
        // policy is an input to hard rule H3, and a male-only ward takes no female patients.
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    // /api/auth/login is rate limited to 20 requests a minute per IP, and this class shares
    // that budget with AuthFlowTests. Logging in once per test spent it and every test here
    // failed on a 429 that looked like a missing access_token. One token per account, reused.
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(email));

        return client;
    }

    private async Task<string> TokenAsync(string email)
    {
        await TokenLock.WaitAsync();

        try
        {
            if (Tokens.TryGetValue(email, out var cached))
            {
                return cached;
            }

            using var client = _application.CreateClient();
            var login = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });

            // Asserted, not assumed: reading access_token off a 429 threw KeyNotFound and
            // pointed at the wrong problem in every test in this class at once.
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var body = await ReadJsonAsync(login);
            var token = body.RootElement.GetProperty("access_token").GetString()!;
            Tokens[email] = token;

            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static Task<HttpResponseMessage> CreateWardAsync(
        HttpClient client, string name, string wardType, string genderPolicy, bool isActive = true)
        => client.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = wardType,
            gender_policy = genderPolicy,
            is_active = isActive
        });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static int CountNamed(JsonDocument wards, string name)
        => wards.RootElement.EnumerateArray()
            .Count(w => w.GetProperty("name").GetString() == name);

    // Unique per test: the fixture's database is shared across the whole collection.
    private static string NewWardName() => $"Test-Ward-{Guid.NewGuid():N}"[..24];
}
