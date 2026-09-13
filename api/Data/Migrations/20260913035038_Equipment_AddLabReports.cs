using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddLabReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lab_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    byte_size = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_reports", x => x.id);
                    table.CheckConstraint("ck_lab_reports_byte_size", "byte_size > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_lab_reports_patient_created_at",
                table: "lab_reports",
                columns: new[] { "patient_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lab_reports");
        }
    }
}
