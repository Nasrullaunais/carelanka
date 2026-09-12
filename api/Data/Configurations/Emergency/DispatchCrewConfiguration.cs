using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class DispatchCrewConfiguration : IEntityTypeConfiguration<DispatchCrew>
{
    public void Configure(EntityTypeBuilder<DispatchCrew> builder)
    {
        builder.ToTable("dispatch_crew");
        builder.HasKey(crew => crew.Id);
        builder.HasOne(crew => crew.Dispatch)
            .WithMany(dispatch => dispatch.Crew)
            .HasForeignKey(crew => crew.DispatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(crew => crew.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(crew => new { crew.DispatchId, crew.StaffMemberId }).IsUnique();
        builder.HasIndex(crew => crew.StaffMemberId);
    }
}
