using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddBed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    has_isolation = table.Column<bool>(type: "boolean", nullable: false),
                    nurse_station_distance = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    asset_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beds", x => x.id);
                    table.CheckConstraint("ck_beds_condition", "condition IN ('usable', 'out_of_service')");
                    table.CheckConstraint("ck_beds_nurse_station_distance", "nurse_station_distance >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_beds_ward_id",
                table: "beds",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "ux_beds_asset_tag",
                table: "beds",
                column: "asset_tag",
                unique: true,
                filter: "is_active AND asset_tag IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_beds_ward_number",
                table: "beds",
                columns: new[] { "ward_id", "bed_number" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beds");
        }
    }
}
