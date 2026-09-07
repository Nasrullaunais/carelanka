using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

/// <summary>
/// Moves the shared <c>/api</c> prefix out of every path and into <c>servers</c>, which is
/// how <c>specs/*.yaml</c> publish it.
/// <para>
/// <strong>This is not cosmetic.</strong> web-ui generates its client with
/// <c>baseUrl: false</c> and sets the base URL to the relative path <c>/api</c> in
/// <c>runtime.ts</c>. If the document also carried <c>/api</c> in each path, every
/// generated call would go to <c>/api/api/auth/login</c> and 404 — and the mistake would
/// only show up at runtime, in the browser, after codegen looked perfectly fine.
/// </para>
/// <para>
/// Controllers keep their real routes (<c>[Route("api/auth")]</c>) so nothing about
/// routing, ASP.NET's own URL generation, or Swagger UI's "Try it out" changes.
/// </para>
/// </summary>
public sealed class ApiPrefixAsServerFilter : IDocumentFilter
{
    private const string Prefix = "/api";

    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Servers =
        [
            new OpenApiServer
            {
                Url = Prefix,
                Description = "Shared CareLanka ASP.NET Core Web API"
            }
        ];

        var rebuilt = new OpenApiPaths();

        foreach (var (path, item) in document.Paths)
        {
            var trimmed = path.StartsWith(Prefix, StringComparison.Ordinal)
                ? path[Prefix.Length..]
                : path;

            rebuilt.Add(string.IsNullOrEmpty(trimmed) ? "/" : trimmed, item);
        }

        document.Paths = rebuilt;
    }
}
