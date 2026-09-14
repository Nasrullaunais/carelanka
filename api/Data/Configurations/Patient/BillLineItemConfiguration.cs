using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BillLineItemConfiguration : IEntityTypeConfiguration<BillLineItem>
{
    public void Configure(EntityTypeBuilder<BillLineItem> builder)
    {
        builder.ToTable("bill_line_items", t =>
        {
            t.HasCheckConstraint(
                "ck_bill_line_items_source", EnumWire.CheckConstraint<BillLineSource>("source"));

            t.HasCheckConstraint("ck_bill_line_items_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_bill_line_items_unit_price", "unit_price >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Source)
            .HasConversion(new SnakeCaseEnumConverter<BillLineSource>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Description).HasMaxLength(200).IsRequired();

        builder.Property(i => i.Quantity).HasPrecision(10, 2);
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);

        builder.Ignore(i => i.LineTotal);

        builder.HasOne(i => i.Bill)
            .WithMany(b => b.LineItems)
            .HasForeignKey(i => i.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.BillId).HasDatabaseName("ix_bill_line_items_bill_id");

        builder.HasQueryFilter(i => i.Bill.Admission.Patient.IsActive);
    }
}
