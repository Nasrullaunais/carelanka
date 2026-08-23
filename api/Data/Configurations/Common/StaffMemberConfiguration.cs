using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.Department).HasMaxLength(100);

        // Scoped WHERE is_active. A plain UNIQUE would be a live bug: deactivate a staff
        // member and you could never reuse their email, and because the global query
        // filter hides the conflicting row the duplicate check in the service would pass
        // and SaveChanges would throw.
        b.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("is_active");

        b.HasIndex(x => x.Role);
    }
}
