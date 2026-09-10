using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class WarningConfiguration : IEntityTypeConfiguration<Warning>
{
    public void Configure(EntityTypeBuilder<Warning> builder)
    {
        builder.ToTable("warnings", t =>
        {
            t.HasCheckConstraint("ck_warnings_type", EnumWire.CheckConstraint<WarningType>("type"));
            t.HasCheckConstraint(
                "ck_warnings_severity", EnumWire.CheckConstraint<WarningSeverity>("severity"));
            t.HasCheckConstraint(
                "ck_warnings_status", EnumWire.CheckConstraint<WarningStatus>("status"));
            t.HasCheckConstraint(
                "ck_warnings_related_entity_type",
                EnumWire.CheckConstraint<RelatedEntityType>("related_entity_type"));
            t.HasCheckConstraint("ck_warnings_raised_by", EnumWire.CheckConstraint<RaisedBy>("raised_by"));
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.RecommendedAction).HasMaxLength(500).IsRequired();

        builder.Property(w => w.Type)
            .HasConversion(new SnakeCaseEnumConverter<WarningType>()).HasMaxLength(30).IsRequired();
        builder.Property(w => w.Severity)
            .HasConversion(new SnakeCaseEnumConverter<WarningSeverity>()).HasMaxLength(20).IsRequired();
        builder.Property(w => w.Status)
            .HasConversion(new SnakeCaseEnumConverter<WarningStatus>()).HasMaxLength(20).IsRequired();
        builder.Property(w => w.RelatedEntityType)
            .HasConversion(new SnakeCaseEnumConverter<RelatedEntityType>()).HasMaxLength(20).IsRequired();
        builder.Property(w => w.RaisedBy)
            .HasConversion(new SnakeCaseEnumConverter<RaisedBy>()).HasMaxLength(10).IsRequired();

        // Every dashboard filters on open warnings.
        builder.HasIndex(w => w.Status);

        // The item detail page asks for one subject's warnings.
        builder.HasIndex(w => new { w.RelatedEntityType, w.RelatedEntityId });

        // No soft delete and no query filter: a warning is closed by moving its status, never
        // by disappearing. The agent-performance report reads dismissed rows.
    }
}
