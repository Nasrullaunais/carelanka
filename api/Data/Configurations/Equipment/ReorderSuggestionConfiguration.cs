using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class ReorderSuggestionConfiguration : IEntityTypeConfiguration<ReorderSuggestion>
{
    public void Configure(EntityTypeBuilder<ReorderSuggestion> builder)
    {
        builder.ToTable("reorder_suggestions", t =>
        {
            t.HasCheckConstraint(
                "ck_reorder_suggestions_source",
                EnumWire.CheckConstraint<ReorderSuggestionSource>("source"));
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Reasoning).HasMaxLength(280);

        builder.Property(s => s.Source)
            .HasConversion(new SnakeCaseEnumConverter<ReorderSuggestionSource>())
            .HasMaxLength(20);

        builder.HasOne<PharmacyItem>()
            .WithMany()
            .HasForeignKey(s => s.PharmacyItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.PharmacyItemId);
    }
}
