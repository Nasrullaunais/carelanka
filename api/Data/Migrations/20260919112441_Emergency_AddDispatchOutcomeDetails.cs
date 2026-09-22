using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddDispatchOutcomeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unacknowledged_alerted_at",
                table: "dispatches");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "dispatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "handover_notes",
                table: "dispatches",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_condition",
                table: "dispatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reassignment_reason",
                table: "dispatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "dispatches",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "handover_notes",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "patient_condition",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "reassignment_reason",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "dispatches");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "unacknowledged_alerted_at",
                table: "dispatches",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
