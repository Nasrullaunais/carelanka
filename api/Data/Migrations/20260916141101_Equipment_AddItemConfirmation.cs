using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddItemConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "awaiting_confirmation",
                table: "equipment_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "equipment_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by_staff_id",
                table: "equipment_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_items_awaiting_confirmation",
                table: "equipment_items",
                column: "awaiting_confirmation",
                filter: "awaiting_confirmation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_equipment_items_awaiting_confirmation",
                table: "equipment_items");

            migrationBuilder.DropColumn(
                name: "awaiting_confirmation",
                table: "equipment_items");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "equipment_items");

            migrationBuilder.DropColumn(
                name: "confirmed_by_staff_id",
                table: "equipment_items");
        }
    }
}
