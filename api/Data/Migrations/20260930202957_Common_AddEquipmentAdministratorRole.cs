using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Common_AddEquipmentAdministratorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ward_staffing_rules_required_role",
                table: "ward_staffing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_members_role",
                table: "staff_members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_required_role",
                table: "shifts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ward_staffing_rules_required_role",
                table: "ward_staffing_rules",
                sql: "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager', 'equipment_administrator')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_members_role",
                table: "staff_members",
                sql: "role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager', 'equipment_administrator')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_required_role",
                table: "shifts",
                sql: "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager', 'equipment_administrator')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ward_staffing_rules_required_role",
                table: "ward_staffing_rules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_members_role",
                table: "staff_members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shifts_required_role",
                table: "shifts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ward_staffing_rules_required_role",
                table: "ward_staffing_rules",
                sql: "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_members_role",
                table: "staff_members",
                sql: "role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shifts_required_role",
                table: "shifts",
                sql: "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");
        }
    }
}
