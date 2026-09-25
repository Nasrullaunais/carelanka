using System.Text.Json;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace CareLanka.Api.Tests;

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
    [InlineData("WorklistStatus")]
    [InlineData("BillLineSource")]
    public async Task Published_enum_values_match_the_contract_in_order(string enumName)
    {
        var generated = await GenerateAsync();

        var expected = Sequence(LoadContract(), "components", "schemas", enumName, "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToArray();
        var actual = generated.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(enumName)
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CreateWardRequest")]
    [InlineData("Ward")]
    [InlineData("CreatePatientRequest")]
    [InlineData("PatientSummary")]
    [InlineData("Patient")]
    [InlineData("PatientDetail")]
    [InlineData("PatientMedicalProfile")]
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
    [InlineData("DischargeCandidate")]
    [InlineData("ChecklistItem")]
    [InlineData("Bill")]
    [InlineData("BillLine")]
    [InlineData("AddBillChargeRequest")]
    [InlineData("OutstandingBill")]
    [InlineData("PreRegisterRequest")]
    [InlineData("MyProfile")]
    [InlineData("MyAdmission")]
    [InlineData("MyAppointment")]
    [InlineData("MyBill")]
    [InlineData("MyBillLine")]
    [InlineData("PatientClaimPreview")]
    [InlineData("BookAppointmentRequest")]
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
    public async Task Patient_self_service_publishes_the_operationIds_the_mobile_client_generates_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        Assert.Equal(
            "preRegisterSelf",
            paths.GetProperty("/me/pre-register").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "getMyProfile",
            paths.GetProperty("/me/profile").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "getMyAdmission",
            paths.GetProperty("/me/admission").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "getMyHistory",
            paths.GetProperty("/me/history").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "bookMyAppointment",
            paths.GetProperty("/me/appointments").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "listMyAppointments",
            paths.GetProperty("/me/appointments").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "cancelMyAppointment",
            paths.GetProperty("/me/appointments/{id}/cancel").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "previewMyClaim",
            paths.GetProperty("/me/claim/preview").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "claimMyRecord",
            paths.GetProperty("/me/claim").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "getMyBill",
            paths.GetProperty("/me/admissions/{admissionId}/bill").GetProperty("get")
                .GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task The_patient_bill_publishes_none_of_the_staff_bill_fields()
    {
        var generated = await GenerateAsync();

        var properties = generated.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("MyBill").GetProperty("properties");

        foreach (var staffOnly in new[]
                 {
                     "raised_by_staff_id", "raised_by_staff_name", "settled_by_staff_id",
                     "settled_by_staff_name", "settlement_note", "patient"
                 })
        {
            Assert.False(properties.TryGetProperty(staffOnly, out _),
                $"MyBill must not publish {staffOnly} - it is a staff-only field.");
        }

        Assert.True(properties.TryGetProperty("is_final", out _));
    }

    [Fact]
    public async Task The_claim_preview_publishes_only_masked_fields()
    {
        var generated = await GenerateAsync();

        var properties = generated.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("PatientClaimPreview").GetProperty("properties");

        // An unmasked name, NIC or address here would hand a stranger holding the slip
        // exactly what the masking exists to withhold.
        foreach (var name in new[] { "full_name", "nic", "address", "date_of_birth", "phone" })
        {
            Assert.False(properties.TryGetProperty(name, out _),
                $"PatientClaimPreview must not publish {name} unmasked.");
        }

        Assert.True(properties.TryGetProperty("masked_full_name", out _));
        Assert.True(properties.TryGetProperty("masked_phone", out _));
    }

    [Fact]
    public async Task Pre_register_publishes_no_admission_shape_and_no_arrival_date()
    {
        var generated = await GenerateAsync();

        var request = generated.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("PreRegisterRequest").GetProperty("properties");

        Assert.False(request.TryGetProperty("expected_arrival", out _));
        Assert.False(request.TryGetProperty("reason_for_visit", out _));

        var responses = generated.RootElement
            .GetProperty("paths").GetProperty("/me/pre-register").GetProperty("post")
            .GetProperty("responses");

        Assert.True(responses.TryGetProperty("200", out var ok));
        Assert.False(responses.TryGetProperty("201", out _));

        Assert.Equal(
            "#/components/schemas/MyProfile",
            ok.GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task Every_self_service_operation_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        Assert.Equal(
            new[] { "200", "400", "401", "403", "409" },
            Responses(paths.GetProperty("/me/pre-register").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "401", "403", "404" },
            Responses(paths.GetProperty("/me/profile").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "401", "403", "404" },
            Responses(paths.GetProperty("/me/admission").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(paths.GetProperty("/me/history").GetProperty("get")));
        Assert.Equal(
            new[] { "201", "400", "401", "403", "409" },
            Responses(paths.GetProperty("/me/appointments").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(paths.GetProperty("/me/appointments").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/me/appointments/{id}/cancel").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/me/claim/preview").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/me/claim").GetProperty("post")));
        Assert.Equal(
            new[] { "200", "401", "403", "404" },
            Responses(paths.GetProperty("/me/admissions/{admissionId}/bill").GetProperty("get")));
    }

    [Fact]
    public async Task Billing_and_discharge_publish_the_operationIds_the_web_client_generates_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        Assert.Equal(
            "listDischargeCandidates",
            paths.GetProperty("/discharges/candidates").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "updateDischargeChecklist",
            paths.GetProperty("/discharges/{admissionId}/checklist").GetProperty("patch")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "confirmDischarge",
            paths.GetProperty("/discharges/{admissionId}/confirm").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "getAdmissionBill",
            paths.GetProperty("/admissions/{admissionId}/bill").GetProperty("get")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "settleBill",
            paths.GetProperty("/admissions/{admissionId}/bill/settle").GetProperty("post")
                .GetProperty("operationId").GetString());
        Assert.Equal(
            "listOutstandingBills",
            paths.GetProperty("/billing/outstanding").GetProperty("get")
                .GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task The_checklist_request_still_publishes_billing_settled_even_though_it_is_refused()
    {
        var generated = await GenerateAsync();

        var properties = generated.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("ChecklistUpdateRequest").GetProperty("properties");

        Assert.True(properties.TryGetProperty("billing_settled", out _));
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

        Assert.Equal(new[] { "get" }, paths.GetProperty("/patient-worklist")
            .EnumerateObject().Select(verb => verb.Name).ToArray());

        Assert.False(paths.TryGetProperty("/admissions/{id}/complete", out _));
    }

    [Fact]
    public async Task The_ward_board_declares_its_failures_and_not_only_its_success()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

        Assert.Equal(
            new[] { "200", "400", "401", "403" },
            Responses(paths.GetProperty("/patient-worklist").GetProperty("get")));
    }

    [Fact]
    public async Task Capacity_routes_publish_the_operationIds_the_other_components_generate_against()
    {
        var generated = await GenerateAsync();
        var paths = generated.RootElement.GetProperty("paths");

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

        Assert.Equal(
            new[] { "200", "400", "401" },
            Responses(paths.GetProperty("/bed-availability").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "401", "404" },
            Responses(paths.GetProperty("/beds/{id}/occupancy").GetProperty("get")));

        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/assign-bed").GetProperty("post")));
    }

    [Fact]
    public async Task The_candidate_list_does_not_squat_on_Equipment_s_bed_register()
    {
        var generated = await GenerateAsync();
        var beds = generated.RootElement.GetProperty("paths").GetProperty("/beds");

        Assert.Equal("listBeds", beds.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("createBed", beds.GetProperty("post").GetProperty("operationId").GetString());
    }

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

        Assert.Equal(
            new[] { "201", "400", "401", "403", "404", "409" },
            Responses(admissions.GetProperty("post")));
        Assert.Equal(
            new[] { "200", "401", "403", "404" },
            Responses(paths.GetProperty("/admissions/{id}").GetProperty("get")));
        Assert.Equal(
            new[] { "200", "400", "401", "403", "404", "409" },
            Responses(paths.GetProperty("/admissions/{id}/details").GetProperty("patch")));

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

    [Fact]
    public async Task AdmissionDetail_omits_only_the_block_that_still_has_no_table_behind_it()
    {
        var generated = await GenerateAsync();
        var properties = generated.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AdmissionDetail")
            .GetProperty("properties");

        Assert.True(properties.TryGetProperty("bed_assignments", out _));

        Assert.True(properties.TryGetProperty("discharge", out _));
        Assert.True(properties.TryGetProperty("bill", out _));

        Assert.False(properties.TryGetProperty("workflows", out _));
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
