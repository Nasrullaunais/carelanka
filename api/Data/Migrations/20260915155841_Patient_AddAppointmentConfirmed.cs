using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddAppointmentConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by_staff_member_id",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_confirmed_by_staff_member_id",
                table: "appointments",
                column: "confirmed_by_staff_member_id");

            // Existing rows carry 'checked_in', which the new constraint would reject. Those
            // bookings ended in an admission, which is what 'completed' with an admission_id
            // now means, so the fact survives the rename intact.
            migrationBuilder.Sql(
                "UPDATE appointments SET status = 'completed' WHERE status = 'checked_in';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments",
                sql: "status IN ('scheduled', 'confirmed', 'completed', 'cancelled', 'no_show')");

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_staff_members_confirmed_by_staff_member_id",
                table: "appointments",
                column: "confirmed_by_staff_member_id",
                principalTable: "staff_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_staff_members_confirmed_by_staff_member_id",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_confirmed_by_staff_member_id",
                table: "appointments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "confirmed_by_staff_member_id",
                table: "appointments");

            migrationBuilder.Sql(
                "UPDATE appointments SET status = 'scheduled' WHERE status = 'confirmed';");

            migrationBuilder.Sql(
                "UPDATE appointments SET status = 'checked_in' WHERE status = 'completed' "
                + "AND admission_id IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments",
                sql: "status IN ('scheduled', 'checked_in', 'completed', 'cancelled', 'no_show')");
        }
    }
}
