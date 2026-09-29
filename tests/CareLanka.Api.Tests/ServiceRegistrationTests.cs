using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CareLanka.Api.Tests;

// Endpoint tests often register their own services, so they can pass while the real app is missing one.
// These tests use Program.cs exactly as it ships, with nothing added.
public sealed class ServiceRegistrationTests
{
    [Fact]
    public async Task Every_registered_service_can_be_built()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new UnchangedApplication();

        // Building the host checks every registration's dependencies and throws if one is missing.
        Assert.NotNull(application.Services);
    }

    [Fact]
    public async Task Every_controller_can_be_built_from_the_real_services()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new UnchangedApplication();

        var feature = new ControllerFeature();
        application.Services.GetRequiredService<ApplicationPartManager>().PopulateFeature(feature);
        Assert.NotEmpty(feature.Controllers);

        var failures = new List<string>();
        foreach (var controller in feature.Controllers.Select(type => type.AsType()))
        {
            using var scope = application.Services.CreateScope();
            try
            {
                ActivatorUtilities.CreateInstance(scope.ServiceProvider, controller);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add($"{controller.Name}: {exception.Message}");
            }

            var fromServices = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(action => action.GetParameters())
                .Where(parameter => parameter.GetCustomAttribute<FromServicesAttribute>() is not null);
            foreach (var parameter in fromServices)
            {
                if (scope.ServiceProvider.GetService(parameter.ParameterType) is null)
                {
                    failures.Add($"{controller.Name}: [FromServices] {parameter.ParameterType.Name} is not registered.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private sealed class UnchangedApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Testing");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
            return base.CreateHost(builder);
        }
    }
}
