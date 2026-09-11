using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddOpenAdmissionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_admissions_open_patient",
                table: "admissions",
                column: "patient_id",
                unique: true,
                filter: "status NOT IN ('discharged', 'cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_admissions_open_patient",
                table: "admissions");
        }
    }
}
