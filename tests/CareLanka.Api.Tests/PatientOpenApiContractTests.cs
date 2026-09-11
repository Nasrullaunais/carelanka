using System.Text.Json;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace CareLanka.Api.Tests;

// The drift gate for Patient Management. patient-spec.yaml is still hand-written, so nothing
// but this test stops the code and the published contract quietly disagreeing. Every schema
// gets a case here as its endpoints are built.
public sealed class PatientOpenApiContractTests
{
    [Theory]
    [InlineData("WardType")]
    [InlineData("GenderPolicy")]
    [InlineData("Gender")]
    [InlineData("AdmissionSource")]
    [InlineData("AdmissionCategory")]
    [InlineData("AdmissionUrgency")]
    [InlineData("AdmissionStatus")]
    [InlineData("AppointmentStatus")]
    [InlineData("AssignmentStatus")]
    [InlineData("AssignedBy")]
    [InlineData("ReleaseReason")]
    [InlineData("WorklistKind")]
    [InlineData("WorklistStatus")]
    public async Task Published_enum_values_match_the_contract_in_order(string enumName)
    {
        var generated = await GenerateAsync();

        var expected = Sequence(LoadContract(), "components", "schemas", enumName, "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToArray();
        var actual = generated.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(enumName)
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();

        // Ordered, not just set-equal: AdmissionCategory's downgrade ladder is an ordinal step,
        // so the order of these lists is part of the contract, not an accident of declaration.
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CreateWardRequest")]
    [InlineData("Ward")]
    [InlineData("CreatePatientRequest")]
    [InlineData("PatientSummary")]
    [InlineData("Patient")]
    [InlineData("PatientDetail")]
    [InlineData("CreateAdmissionRequest")]
    [InlineData("Admission")]
    [InlineData("AdmissionDetail")]
    [InlineData("CancelAdmissionRequest")]
    [InlineData("WardOccupancy")]
    [InlineData("WardCapacitySummary")]
    [InlineData("WardCapacity")]
    [InlineData("Appointment")]
    [InlineData("CreateAppointmentRequest")]
    [InlineData("CheckInRequest")]
    [InlineData("AdmissionBed")]
    [InlineData("BedAssignment")]
    [InlineData("BedOccupancyStatus")]
    [InlineData("AssignBedRequest")]
    [InlineData("WorklistRow")]
    public async Task Published_schema_required_members_match_the_contract(string schemaName)
    {
        var generated = await GenerateAsync();

        var expected = RequiredFromContract(LoadContract(), schemaName);
        var actual = generated.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(schemaName)
            .GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToHashSet();

        Assert.True(expected.SetEquals(actual),
            $"{schemaName} required members differ. Contract: {string.Join(", ", expected.Order())}. "
            + $"Generated: {string.Join(", ", actual.Order())}.");
    }

    [Fact]
    public async Task Ward_routes_publish_the_operationIds_both_frontends_generate_against()
    {
        var generated = await GenerateAsync();
        var wards = generated.RootElement.GetProperty("paths").GetProperty("/wards");

        Assert.Equal("listWards", wards.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createWard", wards.GetProperty("post").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_ward_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var wards = generated.RootElement.GetProperty("paths").GetProperty("/wards");

        // An endpoint declaring only its 200 generates a client that cannot type its failures.
        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(wards.GetProperty("get")));
        Assert.Equal(
            new[] { "201", "400", "401", "403", "409" },
            Responses(wards.GetProperty("post")));
    }

    [Fact]
    public async Task The_ward_board_publishes_the_operationId_the_web_client_generates_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        Assert.Equal(
            "listPatientWorklist",
            paths.GetProperty("/patient-worklist").GetProperty("get")
                .GetProperty("operationId").GetString());

        // No PATCH, no POST, and not by omission. WorklistStatus is derived from two stored
        // statuses, so a write here would be a fourth place a status could change.
        Assert.Equal(new[] { "get" }, paths.GetProperty("/patient-worklist")
            .EnumerateObject().Select(verb => verb.Name).ToArray());

        Assert.Equal(
            "completeVisit",
            paths.GetProperty("/admissions/{id}/complete").GetProperty("post")
                .GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task The_ward_board_and_complete_declare_their_failures_and_not_only_their_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        // An endpoint declaring only its 200 generates a client that cannot type its failures.
        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(paths.GetProperty("/patient-worklist").GetProperty("get")));

        // 409 twice over on complete: the illegal transition, and cl_pat_020 for a visit that
        // has a bed and must be discharged instead. One status, two reasons, both documented.
        Assert.Equal(
            new[] { "200", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/complete").GetProperty("post")));
    }

    [Fact]
    public async Task Capacity_routes_publish_the_operationIds_the_other_components_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        // Two components have been blocked on exactly these two names. Emergency generates
        // getWardCapacity, Staff Management generates getWardOccupancy.
        Assert.Equal(
            "getWardCapacity",
            paths.GetProperty("/capacity/wards").GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "getWardOccupancy",
            paths.GetProperty("/wards/{id}/occupancy").GetProperty("get").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_capacity_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        // No 403 on either: both are AnyStaff, so an authenticated caller is never refused and
        // publishing a 403 would have every client branch on a status that cannot arrive.
        Assert.Equal(
            new[] { "200", "401" },
            Responses(paths.GetProperty("/capacity/wards").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "401", "404" },
            Responses(paths.GetProperty("/wards/{id}/occupancy").GetProperty("get")));
    }

    [Fact]
    public async Task Bed_routes_publish_the_operationIds_both_frontends_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        // getBedOccupancy is the one Equipment Management generates against, and it is what
        // retired the last of their stubs. The other two are ours.
        Assert.Equal(
            "listBedAvailability",
            paths.GetProperty("/bed-availability").GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "getBedOccupancy",
            paths.GetProperty("/beds/{id}/occupancy").GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "assignBedManually",
            paths.GetProperty("/admissions/{id}/assign-bed").GetProperty("post").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_bed_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        // No 403 on either read: both are AnyStaff, so an authenticated caller is never refused
        // and publishing a 403 would have every client branch on a status that cannot arrive.
        // The 400 on the candidate list is real - page and pageSize are range-checked.
        Assert.Equal(
            new[] { "200", "400", "401" },
            Responses(paths.GetProperty("/bed-availability").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "401", "404" },
            Responses(paths.GetProperty("/beds/{id}/occupancy").GetProperty("get")));

        // Assigning has every one of them, and the 403 is the interesting one: which roles may
        // place a patient depends on the bed in the body, so it is a refusal at run time.
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/assign-bed").GetProperty("post")));
    }

    // The candidate list is not /api/beds on purpose - that route is Equipment Management's,
    // and one app cannot have two pages at one address. This pins the split, because the
    // collision would only show up as a startup crash on somebody else's branch.
    [Fact]
    public async Task The_candidate_list_does_not_squat_on_Equipment_s_bed_register()
    {
        var generated = await GenerateAsync();
        var beds = generated.RootElement.GetProperty("paths").GetProperty("/beds");

        Assert.Equal("listBeds", beds.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createBed", beds.GetProperty("post").GetProperty("operationId").GetString());
    }

    // patients_by_category is an open map on the wire, so nothing in the schema pins its keys.
    // This is what stops a rename of the C# enum silently changing them, which for a map is a
    // key that reads as "no patients of that kind" rather than as a break.
    [Fact]
    public void The_care_mix_is_keyed_by_the_published_admission_category_values()
    {
        var published = Sequence(LoadContract(), "components", "schemas", "AdmissionCategory", "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToArray();

        Assert.Equal(published, Enum.GetValues<AdmissionCategory>().Select(EnumWire.ToWire).ToArray());
    }

    [Fact]
    public async Task Patient_routes_publish_the_operationIds_both_frontends_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var patients = paths.GetProperty("/patients");
        var one = paths.GetProperty("/patients/{id}");

        Assert.Equal("listPatients", patients.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createPatient", patients.GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal("getPatient", one.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("updatePatient", one.GetProperty("put").GetProperty("operationId").GetString());
        Assert.Equal(
            "lookupPatient",
            paths.GetProperty("/patients/lookup").GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "linkPatientAccount",
            paths.GetProperty("/patients/{id}/link-account").GetProperty("post").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_patient_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var patients = paths.GetProperty("/patients");
        var one = paths.GetProperty("/patients/{id}");

        // An endpoint declaring only its 200 generates a client that cannot type its failures.
        Assert.Equal(new[] { "200", "400", "401", "403" }, Responses(patients.GetProperty("get")));
        Assert.Equal(new[] { "201", "400", "401", "403", "409" }, Responses(patients.GetProperty("post")));
        Assert.Equal(new[] { "200", "401", "403", "404" }, Responses(one.GetProperty("get")));
        Assert.Equal(new[] { "200", "400", "401", "403", "404", "409" }, Responses(one.GetProperty("put")));
        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(paths.GetProperty("/patients/lookup").GetProperty("post")));
        Assert.Equal(
            new[] { "204", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/patients/{id}/link-account").GetProperty("post")));
    }

    [Fact]
    public async Task Admission_routes_publish_the_operationIds_both_frontends_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var admissions = paths.GetProperty("/admissions");

        Assert.Equal("listAdmissions", admissions.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createAdmission", admissions.GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "getAdmission",
            paths.GetProperty("/admissions/{id}").GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "completeAdmissionDetails",
            paths.GetProperty("/admissions/{id}/details").GetProperty("patch").GetProperty("operationId").GetString());
        Assert.Equal(
            "markArrived",
            paths.GetProperty("/admissions/{id}/arrive").GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "cancelAdmission",
            paths.GetProperty("/admissions/{id}/cancel").GetProperty("post").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_admission_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var admissions = paths.GetProperty("/admissions");

        Assert.Equal(new[] { "200", "400", "401", "403" }, Responses(admissions.GetProperty("get")));

        // 404 as well as the contract's list: the patient or the categorising clinician can be
        // absent, and a client that cannot tell that from a validation failure retries forever.
        Assert.Equal(
            new[] { "201", "400", "401", "403", "404", "409" },
            Responses(admissions.GetProperty("post")));
        Assert.Equal(
            new[] { "200", "401", "403", "404" },
            Responses(paths.GetProperty("/admissions/{id}").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/details").GetProperty("patch")));

        // No 400 on arrive: it takes no body, so there is nothing to fail validation. Cancel
        // has one, because the reason is mandatory and a client can leave it out.
        Assert.Equal(
            new[] { "200", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/arrive").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/cancel").GetProperty("post")));
    }

    [Fact]
    public async Task Appointment_routes_publish_the_operationIds_both_frontends_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var appointments = paths.GetProperty("/appointments");

        Assert.Equal("listAppointments", appointments.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createAppointment", appointments.GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "checkInAppointment",
            paths.GetProperty("/appointments/{id}/check-in").GetProperty("post").GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task Every_appointment_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");
        var appointments = paths.GetProperty("/appointments");

        Assert.Equal(new[] { "200", "400", "401", "403" }, Responses(appointments.GetProperty("get")));
        Assert.Equal(
            new[] { "201", "400", "401", "403", "404", "409" },
            Responses(appointments.GetProperty("post")));
        Assert.Equal(
            new[] { "201", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/appointments/{id}/check-in").GetProperty("post")));
    }

    // Check-in answers with an Admission, not with the booking it consumed. Easy to get wrong
    // in a generated client and invisible until a screen renders the wrong shape.
    [Fact]
    public async Task Checking_in_publishes_an_admission_because_that_is_what_the_desk_works_from_next()
    {
        var generated = await GenerateAsync();

        var schema = generated.RootElement
            .GetProperty("paths").GetProperty("/appointments/{id}/check-in")
            .GetProperty("post").GetProperty("responses").GetProperty("201")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");

        Assert.EndsWith("/Admission", schema.GetProperty("$ref").GetString());
    }

    // The drift gate on the backbone. patient-spec.yaml prints the whole workflow in the
    // description of its IllegalTransition response, so that text is a contract the group and
    // both frontends read. This parses it and holds the service's transition table against it,
    // in both directions - a move added to one and not the other fails here rather than at a
    // viva.
    [Fact]
    public void The_workflow_the_contract_prints_is_the_workflow_the_service_enforces()
    {
        var published = PublishedTransitions();
        var enforced = Enum.GetValues<AdmissionStatus>()
            .SelectMany(from => AdmissionStatusMachine.MovesFrom(from)
                .Select(to => $"{EnumWire.ToWire(from)} -> {EnumWire.ToWire(to)}"))
            .ToHashSet();

        Assert.Equal(published.Order(), enforced.Order());
    }

    /// <summary>
    /// The moves listed in the IllegalTransition response description, as "from -> to" strings.
    /// The lines look like <c>bed_reserved -> admitted, awaiting_bed, cancelled;</c>.
    /// </summary>
    private static HashSet<string> PublishedTransitions()
    {
        var description = ((YamlScalarNode)Map(LoadContract(), "components", "responses", "IllegalTransition")
            .Children[new YamlScalarNode("description")]).Value!;

        var moves = new HashSet<string>();

        foreach (var line in description.Split('\n'))
        {
            if (!line.Contains("->"))
            {
                continue;
            }

            var halves = line.Split("->");
            var from = halves[0].Trim();

            foreach (var to in halves[1].Split(',', StringSplitOptions.TrimEntries))
            {
                moves.Add($"{from} -> {to.TrimEnd(';', '.')}");
            }
        }

        return moves;
    }

    // AdmissionDetail deliberately publishes fewer keys than patient-spec.yaml describes:
    // workflows needs the common AgentWorkflow tables (ADR 3) and discharge needs step 7, so
    // both are omitted rather than returned empty. This pins that, so re-adding them is a
    // decision rather than an accident.
    [Fact]
    public async Task AdmissionDetail_omits_the_two_blocks_that_have_no_table_behind_them_yet()
    {
        var generated = await GenerateAsync();
        var properties = generated.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AdmissionDetail")
            .GetProperty("properties");

        Assert.True(properties.TryGetProperty("bed_assignments", out _));
        Assert.False(properties.TryGetProperty("workflows", out _));
        Assert.False(properties.TryGetProperty("discharge", out _));
    }

    private static string[] Responses(JsonElement operation)
        => operation.GetProperty("responses").EnumerateObject()
            .Select(response => response.Name).Order().ToArray();

    private static async Task<JsonDocument> GenerateAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        return JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
    }

    // The contract nests a response schema's members under allOf, next to the shared
    // AuditFields reference, so the required list is not always at the top level.
    //
    // Every branch counts, not just the first one with a list. Swashbuckle flattens C#
    // inheritance into one schema, so the generated Patient carries PatientSummary's required
    // members as well as its own — reading only one branch compared four members against one
    // and failed for a document that was actually correct.
    private static HashSet<string> RequiredFromContract(YamlMappingNode contract, string schemaName)
    {
        var schema = Map(contract, "components", "schemas", schemaName);
        var required = new HashSet<string>();

        if (schema.Children.TryGetValue(new YamlScalarNode("required"), out var direct))
        {
            required.UnionWith(Values((YamlSequenceNode)direct));
        }

        if (!schema.Children.TryGetValue(new YamlScalarNode("allOf"), out var allOf))
        {
            return required;
        }

        foreach (var branch in ((YamlSequenceNode)allOf).Children.Cast<YamlMappingNode>())
        {
            if (branch.Children.TryGetValue(new YamlScalarNode("$ref"), out var reference))
            {
                required.UnionWith(RequiredFromContract(contract, LastSegment(reference)));
            }

            if (branch.Children.TryGetValue(new YamlScalarNode("required"), out var nested))
            {
                required.UnionWith(Values((YamlSequenceNode)nested));
            }
        }

        return required;
    }

    private static string LastSegment(YamlNode reference)
        => ((YamlScalarNode)reference).Value!.Split('/')[^1];

    private static HashSet<string> Values(YamlSequenceNode node)
        => node.Children.Cast<YamlScalarNode>().Select(item => item.Value!).ToHashSet();

    private static YamlMappingNode LoadContract()
    {
        using var reader = File.OpenText(Path.Combine(
            AppContext.BaseDirectory, "specs", "patient-spec.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static YamlMappingNode Map(YamlMappingNode root, params string[] path)
        => (YamlMappingNode)Follow(root, path);

    private static YamlSequenceNode Sequence(YamlMappingNode root, params string[] path)
        => (YamlSequenceNode)Follow(root, path);

    private static YamlNode Follow(YamlMappingNode root, IEnumerable<string> path)
    {
        YamlNode current = root;

        foreach (var part in path)
        {
            current = ((YamlMappingNode)current).Children[new YamlScalarNode(part)];
        }

        return current;
    }

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Development");
    }
}
