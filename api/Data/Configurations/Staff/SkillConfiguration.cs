using CareLanka.Api.Data.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public const string NameUniqueIndex = "ux_skills_name";

    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500);

        // Partial unique index: two active skills cannot have the same name,
        // but a retired skill's name can be reused.
        builder.HasIndex(s => s.Name)
            .HasDatabaseName(NameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(s => s.IsActive);
    }
}
