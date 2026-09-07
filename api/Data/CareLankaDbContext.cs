using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Data;

/// <summary>
/// One context for the whole application.
/// <para>
/// <strong>Nobody edits <see cref="OnModelCreating"/>.</strong> It is one line, forever.
/// Four people adding configuration to one method means all four get a merge conflict on
/// every migration. Write <c>Data/Configurations/{Component}/*Configuration.cs</c> instead
/// and touch zero shared lines.
/// </para>
/// <para>
/// The <c>DbSet</c> properties below are the one shared surface. Group them by component
/// and add yours at the end of your own group, so a conflict here is at least readable.
/// Columns are <c>snake_case</c> by convention, configured in <c>Program.cs</c> — do not
/// name columns by hand.
/// </para>
/// </summary>
public class CareLankaDbContext : DbContext
{
    public CareLankaDbContext(DbContextOptions<CareLankaDbContext> options) : base(options)
    {
    }

    // ---------- Common (auth, workflow, audit) ----------
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<PatientAccount> PatientAccounts => Set<PatientAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ---------- Emergency / Ambulance ----------

    // ---------- Staff Management ----------

    // ---------- Health Equipment ----------

    // ---------- Patient Management ----------

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
}
