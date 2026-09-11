using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class DischargeChecklistItemConfiguration : IEntityTypeConfiguration<DischargeChecklistItem>
{
    public const string DischargeItemTypeUniqueIndex = "ux_discharge_checklist_items_discharge_item_type";

    public void Configure(EntityTypeBuilder<DischargeChecklistItem> builder)
    {
        builder.ToTable("discharge_checklist_items", t => t.HasCheckConstraint(
            "ck_discharge_checklist_items_item_type",
            EnumWire.CheckConstraint<DischargeChecklistItemType>("item_type")));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemType)
            .HasConversion(new SnakeCaseEnumConverter<DischargeChecklistItemType>())
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.IsMandatory).HasDefaultValue(true);

        builder.Property(i => i.Notes).HasMaxLength(500);

        // Cascade, not Restrict: a checklist item has no meaning without its discharge.
        builder.HasOne(i => i.Discharge)
            .WithMany(d => d.ChecklistItems)
            .HasForeignKey(i => i.DischargeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(i => i.TickedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => i.Discharge.Admission.Patient.IsActive);

        // One row per box. Without this, "tick clinical clearance" twice is two rows and
        // "is it ticked?" has two answers.
        builder.HasIndex(i => new { i.DischargeId, i.ItemType })
            .HasDatabaseName(DischargeItemTypeUniqueIndex)
            .IsUnique();
    }
}
