using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class EquipmentOpenApiContractTests
{
    [Theory]
    [InlineData("/equipment-categories", "get", "listEquipmentCategories")]
    [InlineData("/equipment-categories", "post", "createEquipmentCategory")]
    [InlineData("/equipment-categories/for-removal", "get", "listEquipmentCategoriesForRemoval")]
    [InlineData("/equipment-categories/{id}", "delete", "removeEquipmentCategory")]
    [InlineData("/equipment-items", "get", "listEquipmentItems")]
    [InlineData("/equipment-items", "post", "createEquipmentItem")]
    [InlineData("/equipment-items/{id}", "get", "getEquipmentItem")]
    [InlineData("/equipment-items/{id}", "put", "updateEquipmentItem")]
    [InlineData("/equipment-items/{id}", "delete", "removeEquipmentItem")]
    [InlineData("/equipment-items/by-tag/{assetTag}", "get", "getEquipmentItemByTag")]
    [InlineData("/equipment-items/{id}/assign", "post", "assignEquipmentItem")]
    [InlineData("/equipment-items/{id}/release", "post", "releaseEquipmentItem")]
    [InlineData("/equipment-items/{id}/report-fault", "post", "reportEquipmentFault")]
    [InlineData("/equipment-items/{id}/retire", "post", "retireEquipmentItem")]
    [InlineData("/equipment-items/pending-confirmation", "get", "listEquipmentItemsAwaitingConfirmation")]
    [InlineData("/equipment-items/pending-confirmation/count", "get", "countEquipmentItemsAwaitingConfirmation")]
    [InlineData("/equipment-items/{id}/confirm", "post", "confirmEquipmentItem")]
    [InlineData("/equipment-items/{id}/reject", "post", "rejectEquipmentItem")]
    [InlineData("/beds", "get", "listBeds")]
    [InlineData("/beds", "post", "createBed")]
    [InlineData("/beds/{id}", "patch", "updateBed")]
    [InlineData("/beds/{id}/retire", "post", "retireBed")]
    [InlineData("/lab-reports", "get", "listLabReports")]
    [InlineData("/lab-reports", "post", "uploadLabReport")]
    [InlineData("/lab-reports/{id}/file", "get", "downloadLabReport")]
    [InlineData("/ward-patients", "get", "listWardPatients")]
    [InlineData("/pharmacy-items/{id}", "delete", "removePharmacyItem")]
    [InlineData("/pharmacy-items/{id}/batches", "get", "listPharmacyBatches")]
    [InlineData("/pharmacy-items/{id}/batches", "post", "addPharmacyBatch")]
    [InlineData("/pharmacy-items/{id}/batches/{batchId}/transactions", "post", "recordPharmacyBatchTransaction")]
    [InlineData("/me/prescriptions", "get", "listMyPrescriptions")]
    [InlineData("/me/prescriptions", "post", "uploadMyPrescription")]
    [InlineData("/prescriptions", "get", "listPrescriptions")]
    [InlineData("/prescriptions/{id}/file", "get", "downloadPrescription")]
    [InlineData("/prescriptions/{id}/ready", "post", "markPrescriptionReady")]
    [InlineData("/prescriptions/{id}/deliver", "post", "markPrescriptionDelivered")]
    [InlineData("/prescriptions/{id}/reject", "post", "rejectPrescription")]
    [InlineData("/maintenance-schedules/pending-confirmation", "get", "listMaintenanceSchedulesAwaitingConfirmation")]
    [InlineData("/maintenance-schedules/pending-confirmation/count", "get", "countMaintenanceSchedulesAwaitingConfirmation")]
    [InlineData("/maintenance-schedules/{id}/confirm", "post", "confirmMaintenanceSchedule")]
    [InlineData("/warnings", "get", "listWarnings")]
    [InlineData("/warnings/sweep", "post", "runWarningSweep")]
    [InlineData("/warnings/{id}/acknowledge", "post", "acknowledgeWarning")]
    [InlineData("/warnings/{id}/clear", "post", "clearWarning")]
    public async Task Bed_operation_ids_match_the_contract(string path, string method, string operationId)
    {
        using var document = await GenerateAsync();

        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/equipment-categories", "get")]
    [InlineData("/equipment-categories", "post")]
    [InlineData("/equipment-categories/for-removal", "get")]
    [InlineData("/equipment-categories/{id}", "delete")]
    [InlineData("/equipment-items", "get")]
    [InlineData("/equipment-items", "post")]
    [InlineData("/equipment-items/{id}", "get")]
    [InlineData("/equipment-items/{id}", "put")]
    [InlineData("/equipment-items/{id}", "delete")]
    [InlineData("/equipment-items/by-tag/{assetTag}", "get")]
    [InlineData("/equipment-items/{id}/assign", "post")]
    [InlineData("/equipment-items/{id}/release", "post")]
    [InlineData("/equipment-items/{id}/report-fault", "post")]
    [InlineData("/equipment-items/{id}/retire", "post")]
    [InlineData("/equipment-items/pending-confirmation", "get")]
    [InlineData("/equipment-items/pending-confirmation/count", "get")]
    [InlineData("/equipment-items/{id}/confirm", "post")]
    [InlineData("/equipment-items/{id}/reject", "post")]
    [InlineData("/beds", "get")]
    [InlineData("/beds", "post")]
    [InlineData("/beds/{id}", "patch")]
    [InlineData("/beds/{id}/retire", "post")]
    [InlineData("/lab-reports", "get")]
    [InlineData("/lab-reports", "post")]
    [InlineData("/lab-reports/{id}/file", "get")]
    [InlineData("/ward-patients", "get")]
    [InlineData("/pharmacy-items/{id}", "delete")]
    [InlineData("/pharmacy-items/{id}/batches", "get")]
    [InlineData("/pharmacy-items/{id}/batches", "post")]
    [InlineData("/pharmacy-items/{id}/batches/{batchId}/transactions", "post")]
    [InlineData("/me/prescriptions", "get")]
    [InlineData("/me/prescriptions", "post")]
    [InlineData("/prescriptions", "get")]
    [InlineData("/prescriptions/{id}/file", "get")]
    [InlineData("/prescriptions/{id}/ready", "post")]
    [InlineData("/prescriptions/{id}/deliver", "post")]
    [InlineData("/prescriptions/{id}/reject", "post")]
    [InlineData("/maintenance-schedules/pending-confirmation", "get")]
    [InlineData("/maintenance-schedules/pending-confirmation/count", "get")]
    [InlineData("/maintenance-schedules/{id}/confirm", "post")]
    [InlineData("/warnings", "get")]
    [InlineData("/warnings/sweep", "post")]
    [InlineData("/warnings/{id}/acknowledge", "post")]
    [InlineData("/warnings/{id}/clear", "post")]
    public async Task Every_outcome_the_contract_publishes_is_declared_by_the_code(string path, string method)
    {
        using var document = await GenerateAsync();
        var contract = LoadContract();

        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

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
