using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_ReplaceAdmissionCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_admissions_category",
                table: "admissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admission_fee_rates_category",
                table: "admission_fee_rates");

            migrationBuilder.Sql(
                "UPDATE admissions SET category = 'general' " +
                "WHERE category IN ('hdu', 'inpatient', 'day_case', 'outpatient')");

            migrationBuilder.Sql(
                "UPDATE admission_fee_rates SET category = 'general' " +
                "WHERE category IN ('hdu', 'inpatient', 'day_case', 'outpatient') " +
                "AND NOT EXISTS (" +
                "    SELECT 1 FROM admission_fee_rates existing " +
                "    WHERE existing.category = 'general' AND existing.is_active" +
                ")");

            migrationBuilder.Sql(
                "DELETE FROM admission_fee_rates " +
                "WHERE category IN ('hdu', 'inpatient', 'day_case', 'outpatient')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admissions_category",
                table: "admissions",
                sql: "category IS NULL OR category IN ('icu', 'general', 'surgical', 'maternity', 'emergency')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admission_fee_rates_category",
                table: "admission_fee_rates",
                sql: "category IN ('icu', 'general', 'surgical', 'maternity', 'emergency')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_admissions_category",
                table: "admissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admission_fee_rates_category",
                table: "admission_fee_rates");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admissions_category",
                table: "admissions",
                sql: "category IS NULL OR category IN ('icu', 'hdu', 'inpatient', 'day_case', 'outpatient')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admission_fee_rates_category",
                table: "admission_fee_rates",
                sql: "category IN ('icu', 'hdu', 'inpatient', 'day_case', 'outpatient')");
        }
    }
}
