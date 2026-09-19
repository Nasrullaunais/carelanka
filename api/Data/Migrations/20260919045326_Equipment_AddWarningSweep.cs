using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddWarningSweep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_warnings_raised_by",
                table: "warnings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_maintenance_schedules_created_by",
                table: "maintenance_schedules");

            migrationBuilder.CreateIndex(
                name: "ux_warnings_sweep_live",
                table: "warnings",
                columns: new[] { "type", "related_entity_type", "related_entity_id" },
                unique: true,
                filter: "raised_by = 'system' AND status IN ('open', 'acknowledged')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warnings_raised_by",
                table: "warnings",
                sql: "raised_by IN ('agent', 'user', 'system')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_maintenance_schedules_created_by",
                table: "maintenance_schedules",
                sql: "created_by IN ('agent', 'user', 'system')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_warnings_sweep_live",
                table: "warnings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warnings_raised_by",
                table: "warnings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_maintenance_schedules_created_by",
                table: "maintenance_schedules");

            // The older schema has no word for a sweep warning, so rolling back drops them.
            migrationBuilder.Sql("DELETE FROM warnings WHERE raised_by = 'system';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warnings_raised_by",
                table: "warnings",
                sql: "raised_by IN ('agent', 'user')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_maintenance_schedules_created_by",
                table: "maintenance_schedules",
                sql: "created_by IN ('agent', 'user')");
        }
    }
}
