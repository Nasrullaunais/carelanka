using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class EmergencyOpenApiContractTests
{
    [Theory]
    [InlineData("/ambulances", "get", "listAmbulances")]
    [InlineData("/ambulances", "post", "createAmbulance")]
    [InlineData("/ambulances/{id}", "get", "getAmbulance")]
    [InlineData("/ambulances/{id}", "patch", "updateAmbulance")]
    [InlineData("/ambulances/{id}/retire", "post", "retireAmbulance")]
    [InlineData("/ambulances/{id}/reinstate", "post", "reinstateAmbulance")]
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
