using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PatientEndpointTests
{
    private readonly ApiApplication _application;

    public PatientEndpointTests(ApiApplication application) => _application = application;

    // ---------- registering ----------

    [Fact]
    public async Task A_nurse_registers_a_patient_and_finds_them_again_by_searching_their_nic()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var created = await CreateAsync(client, "Kamala Perera", nic: nic);
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Kamala Perera", body.RootElement.GetProperty("full_name").GetString());
        Assert.Equal(nic, body.RootElement.GetProperty("nic").GetString());
        Assert.False(body.RootElement.GetProperty("has_account").GetBoolean());

        // No account is linked, so no temp reference either — the NIC is the identifier.
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("temp_reference").ValueKind);

        using var found = await ReadJsonAsync(await client.GetAsync($"/api/patients?search={nic}"));

        Assert.Equal(1, found.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(
            nic,
            found.RootElement.GetProperty("items")[0].GetProperty("nic").GetString());
    }

    [Fact]
    public async Task An_arrival_with_no_nic_and_no_phone_is_given_a_temp_reference_rather_than_refused()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var created = await CreateAsync(client, "Unidentified male, approx 40");
        using var body = await ReadJsonAsync(created);
        var reference = body.RootElement.GetProperty("temp_reference").GetString();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.StartsWith($"UNKNOWN-{DateTimeOffset.UtcNow.Year}-", reference);

        // Four digits, so it is short enough to read out loud over a handover.
        Assert.Matches(@"^UNKNOWN-\d{4}-\d{4}$", reference);
    }

    [Fact]
    public async Task Two_unidentified_arrivals_get_different_references_and_neither_reuses_a_number()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        using var first = await ReadJsonAsync(await CreateAsync(client, "Unidentified A"));
        using var second = await ReadJsonAsync(await CreateAsync(client, "Unidentified B"));

        Assert.NotEqual(
            first.RootElement.GetProperty("temp_reference").GetString(),
            second.RootElement.GetProperty("temp_reference").GetString());
    }

    [Fact]
    public async Task A_patient_registered_with_only_a_phone_number_gets_no_temp_reference()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var created = await CreateAsync(client, "Nimal Silva", phone: NewPhone());
        using var body = await ReadJsonAsync(created);

        // The phone is already an identifier. Generating a reference as well would give one
        // person two handles that staff then have to know are the same person.
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("temp_reference").ValueKind);
    }

    [Fact]
    public async Task A_second_record_for_the_same_nic_is_a_409_naming_the_record_that_already_exists()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        using var first = await ReadJsonAsync(await CreateAsync(client, "Sunil Fernando", nic: nic));
        var existingId = first.RootElement.GetProperty("id").GetString()!;

        var duplicate = await CreateAsync(client, "Sunil Fernando", nic: nic);
        using var body = await ReadJsonAsync(duplicate);

        // A returning patient must keep one record with many admissions, not gain a second
        // identity — so the desk is told which record to use instead.
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_pat_002", body.RootElement.GetProperty("code").GetString());
        Assert.Contains(existingId, body.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("male")]
    [InlineData("female")]
    [InlineData("other")]
    [InlineData("unknown")]
    public async Task Every_published_gender_round_trips_on_its_wire_value(string gender)
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var created = await CreateAsync(client, "Gender round trip", nic: NewNic(), gender: gender);
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(gender, body.RootElement.GetProperty("gender").GetString());
    }

    [Fact]
    public async Task A_name_of_nothing_but_spaces_is_a_400_and_not_a_patient_called_space()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.PostAsJsonAsync(
            "/api/patients", new { full_name = "   ", gender = "male", nic = NewNic() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- reading ----------

    [Fact]
    public async Task Getting_a_patient_returns_an_empty_admission_list_rather_than_omitting_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(client, "History Test", NewNic());

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}"));
        var admissions = body.RootElement.GetProperty("admissions");

        // A missing key and an empty list are different things to a generated client.
        Assert.Equal(JsonValueKind.Array, admissions.ValueKind);
        Assert.Empty(admissions.EnumerateArray());
    }

    [Fact]
    public async Task An_unknown_patient_id_is_a_404_carrying_problem_json()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync($"/api/patients/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Paging_reports_the_totals_and_never_shows_the_same_row_on_two_pages()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var marker = $"Paging{Guid.NewGuid():N}"[..16];

        for (var i = 0; i < 3; i++)
        {
            await CreateAsync(client, $"{marker} {i}", nic: NewNic());
        }

        using var first = await ReadJsonAsync(
            await client.GetAsync($"/api/patients?search={marker}&page=1&pageSize=2"));
        using var second = await ReadJsonAsync(
            await client.GetAsync($"/api/patients?search={marker}&page=2&pageSize=2"));

        Assert.Equal(3, first.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("total_pages").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, second.RootElement.GetProperty("items").GetArrayLength());

        Assert.Empty(Ids(first).Intersect(Ids(second)));
    }

    [Fact]
    public async Task A_search_that_matches_nobody_is_page_one_of_one_and_not_page_one_of_zero()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/patients?search=NoSuchPatient{Guid.NewGuid():N}"));

        // Equipment Management shipped `PagedResult` to `main` first and rounds an empty list up
        // to one page, so a UI renders "page 1 of 1" over an empty table rather than "of 0".
        // Patient Management adopted that rather than shipping a second, differing copy.
        Assert.Equal(0, body.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("total_pages").GetInt32());
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Sorting_by_full_name_binds_the_snake_case_value_the_spec_publishes()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var marker = $"Sort{Guid.NewGuid():N}"[..14];

        await CreateAsync(client, $"{marker} Zoysa", nic: NewNic());
        await CreateAsync(client, $"{marker} Abeywardena", nic: NewNic());

        // sortBy=full_name is the first query parameter in the API whose enum value is not one
        // word. Without a type converter the model binder rejects it and this 400s.
        var response = await client.GetAsync(
            $"/api/patients?search={marker}&sortBy=full_name&sortDir=asc");
        using var body = await ReadJsonAsync(response);
        var names = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("full_name").GetString()!).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { $"{marker} Abeywardena", $"{marker} Zoysa" }, names);
    }

    [Fact]
    public async Task An_unknown_sort_field_is_a_400_and_not_a_silently_ignored_parameter()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        // Sorting by something else than what was asked for is worse than refusing: the caller
        // believes the order means something.
        var response = await client.GetAsync("/api/patients?sortBy=date_of_birth");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_page_size_over_the_published_maximum_is_a_400()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync("/api/patients?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- looking up before registering ----------

    [Fact]
    public async Task A_lookup_miss_is_a_200_with_found_false_because_not_knowing_someone_is_normal()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await LookupAsync(client, NewNic());
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("patient").ValueKind);
    }

    [Fact]
    public async Task A_lookup_hit_returns_the_summary_and_says_they_are_not_currently_admitted()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();
        var id = await CreateIdAsync(client, "Returning Patient", nic);

        using var body = await ReadJsonAsync(await LookupAsync(client, nic));

        Assert.True(body.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(id, body.RootElement.GetProperty("patient").GetProperty("id").GetString());

        // Nothing can create an admission yet, so this is false for everyone. It becomes a real
        // signal in the admissions half of step 3.
        Assert.False(body.RootElement.GetProperty("has_open_admission").GetBoolean());
    }

    // ---------- updating ----------

    [Fact]
    public async Task An_update_replaces_the_fields_that_were_missing_at_intake()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();
        var id = await CreateIdAsync(client, "Before Update", nic);

        var response = await client.PutAsJsonAsync($"/api/patients/{id}", new
        {
            full_name = "After Update",
            gender = "female",
            nic,
            phone = NewPhone(),
            address = "42 Galle Road, Colombo 03",
            emergency_contact_name = "A Relative",
            emergency_contact_phone = NewPhone()
        });

        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After Update", body.RootElement.GetProperty("full_name").GetString());
        Assert.Equal("female", body.RootElement.GetProperty("gender").GetString());
        Assert.Equal(
            "42 Galle Road, Colombo 03",
            body.RootElement.GetProperty("address").GetString());
    }

    [Fact]
    public async Task Clearing_every_identifier_on_an_update_gets_a_temp_reference_not_a_500()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(client, "Loses Their Papers", NewNic());

        // The database requires one of NIC, phone or temp reference on every row. Sending an
        // update with neither used to hit that check constraint and surface as a 500.
        var response = await client.PutAsJsonAsync(
            $"/api/patients/{id}", new { full_name = "Loses Their Papers", gender = "unknown" });

        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            @"^UNKNOWN-\d{4}-\d{4}$",
            body.RootElement.GetProperty("temp_reference").GetString());
    }

    [Fact]
    public async Task Updating_one_patient_onto_another_patients_nic_is_a_409()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var taken = NewNic();

        await CreateAsync(client, "Owns The Nic", nic: taken);
        var id = await CreateIdAsync(client, "Wants The Nic", NewNic());

        var response = await client.PutAsJsonAsync(
            $"/api/patients/{id}", new { full_name = "Wants The Nic", gender = "male", nic = taken });

        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_pat_002", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_temp_reference_survives_an_update_that_supplies_a_real_nic()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        using var created = await ReadJsonAsync(await CreateAsync(client, "Named Later"));
        var id = created.RootElement.GetProperty("id").GetString();
        var reference = created.RootElement.GetProperty("temp_reference").GetString();

        var response = await client.PutAsJsonAsync(
            $"/api/patients/{id}", new { full_name = "Named Later", gender = "male", nic = NewNic() });

        using var body = await ReadJsonAsync(response);

        // The wristband and the verbal handover from the unidentified period still have to
        // resolve to this person, so the reference is never cleared once given.
        Assert.Equal(reference, body.RootElement.GetProperty("temp_reference").GetString());
    }

    // ---------- linking a login to a record ----------

    [Fact]
    public async Task A_duty_manager_links_an_account_and_the_record_then_reports_having_one()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var id = await CreateIdAsync(manager, "Has An App", NewNic());
        var accountId = await NewPatientAccountIdAsync();

        var linked = await LinkAsync(manager, id, accountId);
        using var body = await ReadJsonAsync(await manager.GetAsync($"/api/patients/{id}"));

        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        Assert.True(body.RootElement.GetProperty("has_account").GetBoolean());
    }

    [Fact]
    public async Task Linking_a_second_account_to_the_same_record_is_a_409()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var id = await CreateIdAsync(manager, "Already Linked", NewNic());

        await LinkAsync(manager, id, await NewPatientAccountIdAsync());
        var second = await LinkAsync(manager, id, await NewPatientAccountIdAsync());
        using var body = await ReadJsonAsync(second);

        // One account, one record.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_pat_003", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Linking_one_account_to_a_second_record_is_a_409()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var accountId = await NewPatientAccountIdAsync();

        var first = await CreateIdAsync(manager, "First Record", NewNic());
        var second = await CreateIdAsync(manager, "Second Record", NewNic());

        await LinkAsync(manager, first, accountId);
        var reused = await LinkAsync(manager, second, accountId);
        using var body = await ReadJsonAsync(reused);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("cl_pat_004", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Linking_an_account_that_does_not_exist_is_a_404_not_a_foreign_key_500()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var id = await CreateIdAsync(manager, "No Such Login", NewNic());

        var response = await LinkAsync(manager, id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- who may do what ----------

    [Fact]
    public async Task Registering_a_patient_is_403_for_an_administrator_and_401_without_a_token()
    {
        using var anonymous = _application.CreateClient();
        var withoutToken = await CreateAsync(anonymous, "Anonymous", nic: NewNic());

        // An administrator manages wards and reads records; they do not stand at the intake
        // desk. patient-spec.yaml lists nurse, ambulance crew and duty manager only.
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var wrongRole = await CreateAsync(administrator, "Wrong Role", nic: NewNic());

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task A_doctor_may_read_a_patient_record_but_may_not_register_or_edit_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(nurse, "Read Only To Doctor", NewNic());

        // A doctor treats the patient, so they must be able to open the record. They are not
        // at the intake desk and they do not keep the demographics, so both writes are 403.
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var read = await doctor.GetAsync($"/api/patients/{id}");
        var list = await doctor.GetAsync("/api/patients");
        var register = await CreateAsync(doctor, "Doctor Registered", nic: NewNic());
        var edit = await doctor.PutAsJsonAsync(
            $"/api/patients/{id}", new { full_name = "Renamed", gender = "male", nic = NewNic() });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    [Fact]
    public async Task An_administrator_may_read_the_register_but_may_not_edit_a_record()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(nurse, "Read Only To Admin", NewNic());

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var read = await administrator.GetAsync($"/api/patients/{id}");
        var edit = await administrator.PutAsJsonAsync(
            $"/api/patients/{id}", new { full_name = "Renamed", gender = "male", nic = NewNic() });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    [Fact]
    public async Task Linking_an_account_is_403_for_a_nurse_because_it_needs_an_identity_check()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(nurse, "Nurse Cannot Link", NewNic());

        var response = await LinkAsync(nurse, id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_patient_with_no_gender_is_refused_rather_than_recorded_as_male()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = "Gender Omitted",
            nic = NewNic()
        });

        // This one defeats something deliberate. Gender.Unknown exists precisely so hard rule H3,
        // the ward gender-policy filter, behaves deterministically for an unidentified arrival -
        // its own comment in the enum says so. male is declared first, so the old default
        // recorded that patient as male: exactly the case Unknown was added to handle.
        //
        // Status code first. Reading the body first turns a 400 into a KeyNotFoundException and
        // hides which of the two went wrong.
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Updating_a_patient_with_no_gender_is_refused_for_the_same_reason()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreateIdAsync(client, "Gender Omitted On Update", NewNic());

        // UpdatePatientRequest inherits CreatePatientRequest, so the PUT has the same hole and
        // the same fix. It is a PUT, so an omitted key means "clear it" - which for gender would
        // have meant "silently make them male".
        var updated = await client.PutAsJsonAsync($"/api/patients/{id}", new
        {
            full_name = "Gender Omitted On Update"
        });

        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
    }

    // ---------- helpers ----------

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string fullName,
        string? nic = null,
        string? phone = null,
        string gender = "male")
        => client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            nic,
            gender,
            phone
        });

    private static async Task<string> CreateIdAsync(HttpClient client, string fullName, string nic)
    {
        var created = await CreateAsync(client, fullName, nic: nic);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> LookupAsync(HttpClient client, string nic)
        => client.PostAsJsonAsync("/api/patients/lookup", new { nic });

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, string id, Guid accountId)
        => client.PostAsJsonAsync(
            $"/api/patients/{id}/link-account", new { user_account_id = accountId });

    /// <summary>Registers a patient login and returns its PatientAccount.Id, taken off the sign-in response.</summary>
    private async Task<Guid> NewPatientAccountIdAsync()
    {
        using var client = _application.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            phone_number = NewPhone(),
            password = "PatientApp#2026",
            full_name = "App User"
        });

        // Asserted rather than assumed: /api/auth/* is rate limited per IP and shares that
        // budget with every other test class, so a 429 here would otherwise look like a bug
        // in link-account.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("principal").GetProperty("id").GetGuid();
    }

    // One token per account for the whole class. /api/auth/login allows 20 requests a minute
    // per IP and this class shares that budget with AuthFlowTests and WardEndpointTests —
    // logging in per test spends it and every failure then looks like a missing access_token.
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static IEnumerable<string> Ids(JsonDocument page)
        => page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!);

    // Unique per test: the fixture's database is shared across the whole collection.
    private static string NewNic() => $"T{Guid.NewGuid():N}"[..12];

    private static string NewPhone() => $"07{Random.Shared.NextInt64(10000000, 99999999)}";
}
