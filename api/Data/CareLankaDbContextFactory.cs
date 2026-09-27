using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareLanka.Api.Data;

// Used only by the dotnet-ef tools and the migration bundle, so neither has to start the whole API.
// With no connection string configured, the bundle's --connection option supplies it at run time.
public sealed class CareLankaDbContextFactory : IDesignTimeDbContextFactory<CareLankaDbContext>
{
    public CareLankaDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development;

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);

        if (environment == Environments.Development)
        {
            builder.AddUserSecrets<CareLankaDbContextFactory>(optional: true);
        }

        var configuration = builder.AddEnvironmentVariables().Build();

        var options = new DbContextOptionsBuilder<CareLankaDbContext>();
        options.UseCareLankaDatabase(configuration.GetConnectionString(CareLankaDatabase.ConnectionStringName));
        return new CareLankaDbContext(options.Options);
    }
}
