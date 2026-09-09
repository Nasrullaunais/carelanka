using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace CareLanka.Api.Tests;

// The hand-written equipment-spec.yaml is still the design of record, so what the code
// publishes has to be checked against it rather than the other way round. Needs no database:
// it only reads the generated OpenAPI document.
public sealed class EquipmentOpenApiContractTests
{
    [Theory]
    [InlineData("/equipment-categories", "get", "listEquipmentCategories")]
    [InlineData("/equipment-categories", "post", "createEquipmentCategory")]
    [InlineData("/equipment-items", "get", "listEquipmentItems")]
    [InlineData("/equipment-items", "post", "createEquipmentItem")]
    [InlineData("/equipment-items/{id}", "get", "getEquipmentItem")]
    [InlineData("/equipment-items/{id}", "put", "updateEquipmentItem")]
    [InlineData("/equipment-items/by-tag/{assetTag}", "get", "getEquipmentItemByTag")]
    [InlineData("/equipment-items/{id}/assign", "post", "assignEquipmentItem")]
    [InlineData("/equipment-items/{id}/release", "post", "releaseEquipmentItem")]
    [InlineData("/equipment-items/{id}/report-fault", "post", "reportEquipmentFault")]
    [InlineData("/beds", "get", "listBeds")]
    [InlineData("/beds", "post", "createBed")]
    [InlineData("/beds/{id}", "patch", "updateBed")]
    [InlineData("/beds/{id}/retire", "post", "retireBed")]
    public async Task Bed_operation_ids_match_the_contract(string path, string method, string operationId)
    {
        using var document = await GenerateAsync();

        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        // operationId becomes the generated client's function name and is unique across all
        // five specs, so a drift here renames a function in both frontends.
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/equipment-categories", "get")]
    [InlineData("/equipment-categories", "post")]
    [InlineData("/equipment-items", "get")]
    [InlineData("/equipment-items", "post")]
    [InlineData("/equipment-items/{id}", "get")]
    [InlineData("/equipment-items/{id}", "put")]
    [InlineData("/equipment-items/by-tag/{assetTag}", "get")]
    [InlineData("/equipment-items/{id}/assign", "post")]
    [InlineData("/equipment-items/{id}/release", "post")]
    [InlineData("/equipment-items/{id}/report-fault", "post")]
    [InlineData("/beds", "get")]
    [InlineData("/beds", "post")]
    [InlineData("/beds/{id}", "patch")]
    [InlineData("/beds/{id}/retire", "post")]
    public async Task Every_outcome_the_contract_publishes_is_declared_by_the_code(string path, string method)
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();

        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

        // Equality, not a subset. An endpoint that declares only its 200 generates a client
        // that cannot type its failures, and one that declares an outcome the contract does
        // not have is a contract change nobody wrote down.
        Assert.True(
            expected.SetEquals(generated),
            $"{method.ToUpperInvariant()} {path} responses differ. "
            + $"Contract: {string.Join(", ", expected.OrderBy(x => x))}. "
            + $"Generated: {string.Join(", ", generated.OrderBy(x => x))}.");
    }

    [Fact]
    public async Task The_published_bed_shape_carries_every_field_the_contract_promises()
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();

        var generated = Keys(
            document.RootElement, "components", "schemas", "Bed", "properties");

        // Bed is allOf [AuditFields, the bed itself], so both halves are the promise.
        var expected = Keys(Map(contract, "components", "schemas", "AuditFields", "properties"));
        expected.UnionWith(Keys(BedBody(contract, "properties")));

        Assert.True(
            expected.IsSubsetOf(generated),
            $"Bed is missing {string.Join(", ", expected.Except(generated).OrderBy(x => x))}.");
    }

    [Fact]
    public async Task Bed_condition_publishes_exactly_the_two_values_the_contract_names()
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();

        var expected = Sequence(contract, "components", "schemas", "BedCondition", "enum")
            .Children.Cast<YamlScalarNode>().Select(value => value.Value!).ToHashSet();
        var generated = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("BedCondition")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToHashSet();

        // Stored snake_case and published snake_case have to be the same word, or the check
        // constraint and the client disagree about what a legal value is.
        Assert.True(expected.SetEquals(generated),
            $"Contract: {string.Join(", ", expected)}. Generated: {string.Join(", ", generated)}.");
    }

    private static async Task<JsonDocument> GenerateAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();

        return JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
    }

    private static YamlMappingNode BedBody(YamlMappingNode contract, params string[] path)
    {
        // allOf[0] is the AuditFields reference; allOf[1] is the bed's own object.
        var body = (YamlMappingNode)Sequence(contract, "components", "schemas", "Bed", "allOf")
            .Children[1];

        return (YamlMappingNode)Follow(body, path);
    }

    private static YamlMappingNode LoadContract()
    {
        using var reader = File.OpenText(Path.Combine(
            AppContext.BaseDirectory, "specs", "equipment-spec.yaml"));
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
