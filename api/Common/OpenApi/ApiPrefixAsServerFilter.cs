using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

// Moves /api out of every path and into servers, which is how specs/*.yaml publish it.
// Leave it in the paths and web-ui's relative base URL makes every call /api/api/... at runtime.
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
