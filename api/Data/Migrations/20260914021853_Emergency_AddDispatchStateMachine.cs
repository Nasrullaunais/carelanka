using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddDispatchStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_dispatches_active_ambulance",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acknowledged_at",
                table: "dispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "acknowledged_by_staff_id",
                table: "dispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "declined_reason",
                table: "dispatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "unacknowledged_alerted_at",
                table: "dispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_dispatches_active_ambulance",
                table: "dispatches",
                column: "ambulance_id",
                unique: true,
                filter: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital')");

            migrationBuilder.CreateIndex(
                name: "ux_dispatches_active_emergency_call",
                table: "dispatches",
                column: "emergency_call_id",
                unique: true,
                filter: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital', 'handed_over', 'declined', 'cancelled', 'reassigned')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_dispatches_active_ambulance",
                table: "dispatches");

            migrationBuilder.DropIndex(
                name: "ux_dispatches_active_emergency_call",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "acknowledged_at",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "acknowledged_by_staff_id",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "declined_reason",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "unacknowledged_alerted_at",
                table: "dispatches");

            migrationBuilder.CreateIndex(
                name: "ux_dispatches_active_ambulance",
                table: "dispatches",
                column: "ambulance_id",
                unique: true,
                filter: "status IN ('assigned', 'en_route')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'en_route', 'completed', 'cancelled', 'reassigned')");
        }
    }
}
