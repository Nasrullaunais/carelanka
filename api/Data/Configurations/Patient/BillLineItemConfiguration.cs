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

            // Money, so the database says so too. A negative quantity or price is a discount,
            // and a discount is a decision nobody in this component is allowed to make.
            t.HasCheckConstraint("ck_bill_line_items_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_bill_line_items_unit_price", "unit_price >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Source)
            .HasConversion(new SnakeCaseEnumConverter<BillLineSource>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Description).HasMaxLength(200).IsRequired();

        // numeric, never double. A rupee is not representable in binary floating point, and a
        // bill that is off by a cent is a bill somebody argues about at the counter.
        builder.Property(i => i.Quantity).HasPrecision(10, 2);
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);

        // LineTotal is Quantity x UnitPrice and nothing else, so there is no column for it.
        builder.Ignore(i => i.LineTotal);

        // Cascade, not Restrict: a line has no meaning without its bill. Same call as the
        // discharge checklist items.
        builder.HasOne(i => i.Bill)
            .WithMany(b => b.LineItems)
            .HasForeignKey(i => i.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.BillId).HasDatabaseName("ix_bill_line_items_bill_id");

        builder.HasQueryFilter(i => i.Bill.Admission.Patient.IsActive);
    }
}
