using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_RemoveMedicalProfileRecentSituation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Staff-written notes are folded into current_symptoms rather than dropped. A merge that
            // would not fit stops the migration instead of cutting clinical text short.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM patient_medical_profiles
                        WHERE btrim(coalesce(recent_situation, '')) <> ''
                          AND length(concat_ws(E'\n\n', nullif(btrim(current_symptoms), ''), btrim(recent_situation))) > 2000
                    ) THEN
                        RAISE EXCEPTION 'A medical profile''s current symptoms and recent situation together exceed 2000 characters. Shorten it by hand, then run this migration again.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE patient_medical_profiles
                SET current_symptoms = concat_ws(E'\n\n', nullif(btrim(current_symptoms), ''), btrim(recent_situation))
                WHERE btrim(coalesce(recent_situation, '')) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "recent_situation",
                table: "patient_medical_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recent_situation",
                table: "patient_medical_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
