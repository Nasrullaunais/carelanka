using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class EmergencyOpenApiContractTests
{
    [Theory]
    [InlineData("/ambulances/{id}/crew", "get", "getCurrentAmbulanceCrew")]
    [InlineData("/ambulances/{id}/crew", "post", "assignCurrentAmbulanceCrew")]
    [InlineData("/ambulances/{ambulanceId}/crew/{staffMemberId}", "delete", "unassignCurrentAmbulanceCrew")]
    [InlineData("/emergency-calls/{id}/dispatch", "post", "dispatchEmergencyCall")]
    [InlineData("/me/dispatches/{id}/acknowledge", "post", "acknowledgeMyDispatch")]
    [InlineData("/me/dispatches/{id}/decline", "post", "declineMyDispatch")]
    [InlineData("/me/emergency-calls/{id}/cancel", "post", "cancelMyEmergencyCall")]
    [InlineData("/me/emergency-calls/{id}/cancellation-request", "post", "requestMyEmergencyCallCancellation")]
    [InlineData("/emergency-cancellation-requests", "get", "listEmergencyCancellationRequests")]
    [InlineData("/emergency-calls/{id}/cancellation-request/approve", "post", "approveEmergencyCancellationRequest")]
    [InlineData("/emergency-calls/{id}/cancellation-request/reject", "post", "rejectEmergencyCancellationRequest")]
    public void Phase_zero_operations_are_published(
        string path,
        string method,
        string operationId)
    {
        var contract = LoadContract();
        Assert.Equal(operationId, Scalar(contract, "paths", path, method, "operationId"));
    }

    [Fact]
    public void Dispatch_status_values_match_the_aligned_state_machine()
    {
        var contract = LoadContract();
        var actual = Sequence(contract, "components", "schemas", "DispatchStatus", "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToArray();
        var expected = new[]
        {
            "assigned",
            "acknowledged",
            "en_route_to_scene",
            "at_scene",
            "transporting_to_hospital",
            "handed_over",
            "declined",
            "cancelled",
            "reassigned"
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Responding_crew_is_read_only_and_navigation_launches_google_maps()
    {
        var contract = LoadContract();
        var dispatchCrew = Map(contract, "paths", "/dispatches/{id}/crew");
        var navigation = Map(contract, "components", "schemas", "NavigationTarget", "properties");

        Assert.True(dispatchCrew.Children.ContainsKey(new YamlScalarNode("get")));
        Assert.False(dispatchCrew.Children.ContainsKey(new YamlScalarNode("post")));
        Assert.False(Map(contract, "paths").Children.ContainsKey(
            new YamlScalarNode("/dispatches/{dispatchId}/crew/{staffMemberId}")));
        Assert.True(navigation.Children.ContainsKey(new YamlScalarNode("google_maps_url")));
        Assert.False(navigation.Children.ContainsKey(new YamlScalarNode("steps")));
        Assert.False(navigation.Children.ContainsKey(new YamlScalarNode("encoded_polyline")));
    }

    [Fact]
    public void Eligibility_and_patient_tracking_shapes_preserve_the_phase_zero_boundaries()
    {
        var contract = LoadContract();
        var ambulance = Map(contract, "components", "schemas", "AmbulanceSummary", "properties");
        var tracking = Map(contract, "components", "schemas", "MyCallTracking", "properties");
        var dispatch = Map(contract, "components", "schemas", "Dispatch", "properties");

        Assert.True(ambulance.Children.ContainsKey(new YamlScalarNode("current_crew_count")));
        Assert.True(ambulance.Children.ContainsKey(new YamlScalarNode("required_crew_count")));
        Assert.True(ambulance.Children.ContainsKey(new YamlScalarNode("eligibility_block_reasons")));
        Assert.True(tracking.Children.ContainsKey(new YamlScalarNode("cancellation_request_status")));
        Assert.False(tracking.Children.ContainsKey(new YamlScalarNode("crew")));
        Assert.False(tracking.Children.ContainsKey(new YamlScalarNode("details")));
        Assert.False(tracking.Children.ContainsKey(new YamlScalarNode("rationale")));
        Assert.False(dispatch.Children.ContainsKey(new YamlScalarNode("destination_ward_id")));
    }

    [Theory]
    [InlineData("/ambulances", "get", "listAmbulances")]
    [InlineData("/ambulances", "post", "createAmbulance")]
    [InlineData("/ambulances/{id}", "get", "getAmbulance")]
    [InlineData("/ambulances/{id}", "patch", "updateAmbulance")]
    [InlineData("/ambulances/{id}/retire", "post", "retireAmbulance")]
    [InlineData("/ambulances/{id}/reinstate", "post", "reinstateAmbulance")]
    [InlineData("/ambulances/{id}/crew", "get", "getCurrentAmbulanceCrew")]
    [InlineData("/ambulances/{id}/crew", "post", "assignCurrentAmbulanceCrew")]
    [InlineData("/ambulances/{ambulanceId}/crew/{staffMemberId}", "delete", "unassignCurrentAmbulanceCrew")]
    public async Task Ambulance_operation_ids_match_the_contract(
        string path,
        string method,
        string operationId)
    {
        using var document = await GenerateAsync();
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/ambulances", "get")]
    [InlineData("/ambulances", "post")]
    [InlineData("/ambulances/{id}", "get")]
    [InlineData("/ambulances/{id}", "patch")]
    [InlineData("/ambulances/{id}/retire", "post")]
    [InlineData("/ambulances/{id}/reinstate", "post")]
    [InlineData("/ambulances/{id}/crew", "get")]
    [InlineData("/ambulances/{id}/crew", "post")]
    [InlineData("/ambulances/{ambulanceId}/crew/{staffMemberId}", "delete")]
    public async Task Ambulance_response_statuses_match_the_contract(string path, string method)
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();
        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

        Assert.True(expected.SetEquals(generated),
            $"{method.ToUpperInvariant()} {path}: contract [{string.Join(", ", expected)}], "
            + $"generated [{string.Join(", ", generated)}]");
    }

    [Fact]
    public async Task Ambulance_status_values_match_the_contract()
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();
        var expected = Sequence(contract, "components", "schemas", "AmbulanceStatus", "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToHashSet();
        var generated = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("AmbulanceStatus")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToHashSet();

        Assert.True(expected.SetEquals(generated));
    }

    [Fact]
    public async Task Phase_one_eligibility_shape_matches_the_contract()
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();
        var expectedReasons = Sequence(
                contract,
                "components",
                "schemas",
                "AmbulanceEligibilityBlockReason",
                "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToHashSet();
        var generatedReasons = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("AmbulanceEligibilityBlockReason").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString()!).ToHashSet();
        var expectedFields = Keys(Map(
            contract,
            "components",
            "schemas",
            "AmbulanceSummary",
            "properties"));
        var generatedFields = Keys(
            document.RootElement,
            "components",
            "schemas",
            "AmbulanceSummary",
            "properties");

        Assert.True(expectedReasons.SetEquals(generatedReasons));
        Assert.True(expectedFields.SetEquals(generatedFields));
    }

    [Fact]
    public async Task Phase_one_request_and_query_wire_contract_is_generated()
    {
        using var document = await GenerateAsync();
        var operation = document.RootElement.GetProperty("paths").GetProperty("/ambulances")
            .GetProperty("get");
        var parameterNames = operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()!).ToHashSet();
        var assign = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("AssignAmbulanceCrewRequest");

        Assert.Contains("eligibleOnly", parameterNames);
        Assert.Contains("nearToLatitude", parameterNames);
        Assert.Contains("nearToLongitude", parameterNames);
        Assert.Contains("staff_member_id", assign.GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()));
    }

    private static async Task<JsonDocument> GenerateAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
    }

    private static YamlMappingNode LoadContract()
    {
        using var reader = File.OpenText(Path.Combine(
            AppContext.BaseDirectory, "specs", "emergency-spec.yaml"));
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

    private static YamlSequenceNode Sequence(YamlMappingNode root, params string[] path)
        => (YamlSequenceNode)Follow(root, path);

    private static string Scalar(YamlMappingNode root, params string[] path)
        => ((YamlScalarNode)Follow(root, path)).Value!;

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
}
