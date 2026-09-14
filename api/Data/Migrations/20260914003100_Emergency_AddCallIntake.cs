using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddCallIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                table: "emergency_calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "location_accuracy_metres",
                table: "emergency_calls",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "location_captured_at",
                table: "emergency_calls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE emergency_calls
                SET idempotency_key = gen_random_uuid(),
                    location_captured_at = created_at
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "idempotency_key",
                table: "emergency_calls",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "location_captured_at",
                table: "emergency_calls",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_emergency_calls_idempotency_key",
                table: "emergency_calls",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_emergency_calls_idempotency_key",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "location_accuracy_metres",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "location_captured_at",
                table: "emergency_calls");
        }
    }
}
