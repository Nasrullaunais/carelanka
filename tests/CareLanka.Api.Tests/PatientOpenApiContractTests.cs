using System.Text.Json;
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
    private static HashSet<string> RequiredFromContract(YamlMappingNode contract, string schemaName)
    {
        var schema = Map(contract, "components", "schemas", schemaName);

        if (schema.Children.TryGetValue(new YamlScalarNode("required"), out var direct))
        {
            return Values((YamlSequenceNode)direct);
        }

        var branches = (YamlSequenceNode)schema.Children[new YamlScalarNode("allOf")];

        foreach (var branch in branches.Children.Cast<YamlMappingNode>())
        {
            if (branch.Children.TryGetValue(new YamlScalarNode("required"), out var nested))
            {
                return Values((YamlSequenceNode)nested);
            }
        }

        return [];
    }

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
