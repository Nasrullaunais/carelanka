using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
{
    public const string ActiveAmbulanceUniqueIndex = "ux_dispatches_active_ambulance";

    public void Configure(EntityTypeBuilder<Dispatch> builder)
    {
        builder.ToTable("dispatches", table =>
            table.HasCheckConstraint("ck_dispatches_status", EnumWire.CheckConstraint<DispatchStatus>("status")));

        builder.HasKey(dispatch => dispatch.Id);
        builder.Property(dispatch => dispatch.Status)
            .HasConversion(new SnakeCaseEnumConverter<DispatchStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(dispatch => dispatch.DispatchedAt).IsRequired();

        builder.HasOne(dispatch => dispatch.EmergencyCall)
            .WithMany(call => call.Dispatches)
            .HasForeignKey(dispatch => dispatch.EmergencyCallId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(dispatch => dispatch.Ambulance)
            .WithMany(ambulance => ambulance.Dispatches)
            .HasForeignKey(dispatch => dispatch.AmbulanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ward>()
            .WithMany()
            .HasForeignKey(dispatch => dispatch.DestinationWardId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(dispatch => dispatch.SupersededByDispatch)
            .WithMany()
            .HasForeignKey(dispatch => dispatch.SupersededByDispatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dispatch => dispatch.AmbulanceId)
            .HasDatabaseName(ActiveAmbulanceUniqueIndex)
            .IsUnique()
            .HasFilter("status IN ('assigned', 'en_route')");
        builder.HasIndex(dispatch => dispatch.EmergencyCallId);
        builder.HasIndex(dispatch => new { dispatch.Status, dispatch.DispatchedAt });
    }
}
