using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.StaffMemberId);

        b.HasOne(x => x.StaffMember)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // StaffMember is soft-deletable, so it carries a global query filter. Without a
        // matching one here, a deactivated staff member's tokens would still be visible
        // while their owner row was invisible. Deactivating someone now ends their
        // sessions, which is the point of storing refresh tokens at all.
        b.HasQueryFilter(x => x.StaffMember!.IsActive);
    }
}
