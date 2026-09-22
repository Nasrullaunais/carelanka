using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddTrackingCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_request_reason",
                table: "emergency_calls",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_request_status",
                table: "emergency_calls",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_requested_at",
                table: "emergency_calls",
                type: "timestamp with time zone",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_request_reason",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "cancellation_request_status",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                table: "emergency_calls");

        }
    }
}
