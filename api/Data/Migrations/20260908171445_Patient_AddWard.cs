using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddWard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gender_policy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wards", x => x.id);
                    table.CheckConstraint("ck_wards_gender_policy", "gender_policy IN ('male', 'female', 'mixed')");
                    table.CheckConstraint("ck_wards_type", "ward_type IN ('icu', 'hdu', 'general', 'maternity', 'pediatric', 'isolation')");
                });

            migrationBuilder.CreateIndex(
                name: "ux_wards_name",
                table: "wards",
                column: "name",
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wards");
        }
    }
}
