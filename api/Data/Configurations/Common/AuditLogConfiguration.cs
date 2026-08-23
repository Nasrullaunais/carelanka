using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();

        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.PerformedByStaffMemberId);
        b.HasIndex(x => x.CreatedAt);

        // No FK to StaffMember on purpose: the log must survive the staff member's row.
    }
}
