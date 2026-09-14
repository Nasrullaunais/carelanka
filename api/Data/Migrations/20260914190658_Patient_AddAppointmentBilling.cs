using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddAppointmentBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bills_admission_id",
                table: "bills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_line_items_source",
                table: "bill_line_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "admission_id",
                table: "bills",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "appointment_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "appointments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_staff_member_id",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_bills_admission_id",
                table: "bills",
                column: "admission_id",
                unique: true,
                filter: "admission_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_bills_appointment_id",
                table: "bills",
                column: "appointment_id",
                unique: true,
                filter: "appointment_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bills_one_owner",
                table: "bills",
                sql: "(admission_id IS NULL) <> (appointment_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_line_items_source",
                table: "bill_line_items",
                sql: "source IN ('admission_fee', 'bed_stay', 'consultation_fee', 'manual')");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_cancelled_by_staff_member_id",
                table: "appointments",
                column: "cancelled_by_staff_member_id");

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_staff_members_cancelled_by_staff_member_id",
                table: "appointments",
                column: "cancelled_by_staff_member_id",
                principalTable: "staff_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_bills_appointments_appointment_id",
                table: "bills",
                column: "appointment_id",
                principalTable: "appointments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_staff_members_cancelled_by_staff_member_id",
                table: "appointments");

            migrationBuilder.DropForeignKey(
                name: "fk_bills_appointments_appointment_id",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "ux_bills_admission_id",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "ux_bills_appointment_id",
                table: "bills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bills_one_owner",
                table: "bills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_line_items_source",
                table: "bill_line_items");

            migrationBuilder.DropIndex(
                name: "ix_appointments_cancelled_by_staff_member_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "appointment_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "cancelled_by_staff_member_id",
                table: "appointments");

            migrationBuilder.AlterColumn<Guid>(
                name: "admission_id",
                table: "bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_bills_admission_id",
                table: "bills",
                column: "admission_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_line_items_source",
                table: "bill_line_items",
                sql: "source IN ('admission_fee', 'bed_stay', 'manual')");
        }
    }
}
