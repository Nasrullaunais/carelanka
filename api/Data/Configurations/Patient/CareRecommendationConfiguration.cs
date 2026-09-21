using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class CareRecommendationConfiguration : IEntityTypeConfiguration<CareRecommendation>
{
    public const int ReportedTextLength = 2000;
    public const int MessageLength = 2000;
    public const int RejectionReasonLength = 1000;

    public void Configure(EntityTypeBuilder<CareRecommendation> builder)
    {
        builder.ToTable("care_recommendations");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.ReportedText).HasMaxLength(ReportedTextLength).IsRequired();
        builder.Property(row => row.AgentMessage).HasMaxLength(MessageLength);
        builder.Property(row => row.DoctorMessage).HasMaxLength(MessageLength);
        builder.Property(row => row.RejectionReason).HasMaxLength(RejectionReasonLength);

        builder.HasOne(row => row.Patient)
            .WithMany()
            .HasForeignKey(row => row.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(row => row.Admission)
            .WithMany()
            .HasForeignKey(row => row.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(row => row.ReviewedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // What the review queue filters on, and what the agent reads for a patient's past
        // reports (patient-management-plan.md section 8.11's patient_history.past_recommendations).
        builder.HasIndex(row => new { row.PatientId, row.ReportedAt });
        builder.HasIndex(row => row.Status);

        builder.HasQueryFilter(row => row.Patient.IsActive);
    }
}
