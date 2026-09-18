using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BedSuggestionCandidateConfiguration : IEntityTypeConfiguration<BedSuggestionCandidate>
{
    public const string RankUniqueIndex = "ux_bed_suggestion_candidates_rank";

    public void Configure(EntityTypeBuilder<BedSuggestionCandidate> builder)
    {
        builder.ToTable("bed_suggestion_candidates");

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Rationale).HasMaxLength(500);

        builder.Property(candidate => candidate.RulesSatisfied)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();

        builder.HasOne(candidate => candidate.BedSuggestion)
            .WithMany(suggestion => suggestion.Candidates)
            .HasForeignKey(candidate => candidate.BedSuggestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(candidate => new { candidate.BedSuggestionId, candidate.Rank })
            .HasDatabaseName(RankUniqueIndex)
            .IsUnique();
    }
}
