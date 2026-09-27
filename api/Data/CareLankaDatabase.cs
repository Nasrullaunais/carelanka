using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Data;

public static class CareLankaDatabase
{
    public const string ConnectionStringName = "CareLanka";

    public static DbContextOptionsBuilder UseCareLankaDatabase(
        this DbContextOptionsBuilder options, string? connectionString)
        => (connectionString is null ? options.UseNpgsql() : options.UseNpgsql(connectionString))
            .UseSnakeCaseNamingConvention();
}
