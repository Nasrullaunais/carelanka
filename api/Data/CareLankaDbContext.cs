using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Data;

public class CareLankaDbContext : DbContext
{
    public CareLankaDbContext(DbContextOptions<CareLankaDbContext> options) : base(options)
    {
    }

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<PatientAccount> PatientAccounts => Set<PatientAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Equipment Management
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<EquipmentCategory> EquipmentCategories => Set<EquipmentCategory>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<Warning> Warnings => Set<Warning>();

    // Patient Management
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<BedAssignment> BedAssignments => Set<BedAssignment>();
    public DbSet<Discharge> Discharges => Set<Discharge>();
    public DbSet<DischargeChecklistItem> DischargeChecklistItems => Set<DischargeChecklistItem>();

    // Never add configuration here. Write Data/Configurations/{Component}/ instead,
    // or all four of us conflict on this method every migration.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
}
