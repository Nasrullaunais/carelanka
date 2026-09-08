using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Generated_auth_contract_keeps_anonymous_security_responses_and_required_members()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var contract = LoadContract();

        Assert.False(root.TryGetProperty("security", out _));

        foreach (var path in new[] { "/auth/login", "/auth/patient/register", "/auth/patient/login", "/auth/refresh", "/health" })
        {
            var operation = paths.GetProperty(path).EnumerateObject().Single().Value;
            Assert.False(operation.TryGetProperty("security", out _));
        }

        var protectedSecurity = paths.GetProperty("/auth/me").GetProperty("get")
            .GetProperty("security")[0].EnumerateObject().Single();
        Assert.Equal("Bearer", protectedSecurity.Name);

        var expectedRefreshResponses = Keys(Map(contract, "paths", "/auth/refresh", "post", "responses"));
        var generatedRefreshResponses = paths.GetProperty("/auth/refresh").GetProperty("post")
            .GetProperty("responses").EnumerateObject().Select(response => response.Name).ToHashSet();
        Assert.True(expectedRefreshResponses.SetEquals(generatedRefreshResponses));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        AssertRequiredMatchesContract(schemas.GetProperty("AuthTokens"), contract, "AuthTokens");
        AssertRequiredMatchesContract(schemas.GetProperty("CurrentPrincipal"), contract, "CurrentPrincipal");
    }

    private static void AssertRequiredMatchesContract(
        JsonElement generatedSchema, YamlMappingNode contract, string schemaName)
    {
        var generated = generatedSchema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet();
        var expected = Sequence(contract, "components", "schemas", schemaName, "required")
            .Children.Cast<YamlScalarNode>().Select(item => item.Value!).ToHashSet();

        Assert.True(expected.SetEquals(generated),
            $"{schemaName} required members differ. Expected: {string.Join(", ", expected)}. "
            + $"Generated: {string.Join(", ", generated)}.");

        var properties = generatedSchema.GetProperty("properties");
        foreach (var member in expected)
        {
            var property = properties.GetProperty(member);
            Assert.False(property.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean(),
                $"{schemaName}.{member} is required by common-spec.yaml but generated as nullable.");
        }
    }

    private static YamlMappingNode LoadContract()
    {
        using var reader = File.OpenText(Path.Combine(
            AppContext.BaseDirectory, "specs", "common-spec.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static HashSet<string> Keys(YamlMappingNode node)
        => node.Children.Keys.Cast<YamlScalarNode>().Select(key => key.Value!).ToHashSet();

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
