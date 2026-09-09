using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Data;

public class CareLankaDbContext : DbContext
{
    public CareLankaDbContext(DbContextOptions<CareLankaDbContext> options) : base(options)
    {
    }

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<PatientAccount> PatientAccounts => Set<PatientAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Patient Management
    public DbSet<Ward> Wards => Set<Ward>();

    // Never add configuration here. Write Data/Configurations/{Component}/ instead,
    // or all four of us conflict on this method every migration.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
}
