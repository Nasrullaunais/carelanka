using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CareLanka.Api.Tests;

// Starting the API costs a quarter of a second and the document never changes within a run,
// so every contract test shares one copy. Each caller gets its own JsonDocument to dispose.
internal static class GeneratedOpenApi
{
    private static readonly Lazy<Task<string>> Document = new(GenerateAsync);

    public static async Task<JsonDocument> ParseAsync() => JsonDocument.Parse(await Document.Value);

    private static async Task<string> GenerateAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private sealed class SwaggerOnlyApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Development");
    }
}
