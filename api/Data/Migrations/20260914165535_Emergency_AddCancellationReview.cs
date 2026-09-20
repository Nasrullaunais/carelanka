using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddCancellationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_review_notes",
                table: "emergency_calls",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_reviewed_at",
                table: "emergency_calls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancellation_reviewed_by_staff_id",
                table: "emergency_calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_emergency_calls_cancellation_reviewed_by_staff_id",
                table: "emergency_calls",
                column: "cancellation_reviewed_by_staff_id");

            migrationBuilder.AddForeignKey(
                name: "fk_emergency_calls_staff_members_cancellation_reviewed_by_staf",
                table: "emergency_calls",
                column: "cancellation_reviewed_by_staff_id",
                principalTable: "staff_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_emergency_calls_staff_members_cancellation_reviewed_by_staf",
                table: "emergency_calls");

            migrationBuilder.DropIndex(
                name: "ix_emergency_calls_cancellation_reviewed_by_staff_id",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "cancellation_review_notes",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "cancellation_reviewed_at",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "cancellation_reviewed_by_staff_id",
                table: "emergency_calls");
        }
    }
}
