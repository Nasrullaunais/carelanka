using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Common_AddStaffDoctorProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "joining_date",
                table: "staff_members",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_number",
                table: "staff_members",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specialization",
                table: "staff_members",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "staff_members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_staff_members_registration_number",
                table: "staff_members",
                column: "registration_number",
                unique: true,
                filter: "is_active AND registration_number IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_members_title",
                table: "staff_members",
                sql: "title IN ('mr', 'mrs', 'ms', 'miss', 'dr', 'prof')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_staff_members_registration_number",
                table: "staff_members");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_members_title",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "joining_date",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "registration_number",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "specialization",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "title",
                table: "staff_members");
        }
    }
}
