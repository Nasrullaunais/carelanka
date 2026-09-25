using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddAmbulanceStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ambulance_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ambulance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ambulance_status_history", x => x.id);
                    table.CheckConstraint("ck_ambulance_status_history_status", "status IN ('available', 'dispatched', 'en_route', 'at_scene', 'transporting', 'out_of_service')");
                    table.ForeignKey(
                        name: "fk_ambulance_status_history_ambulances_ambulance_id",
                        column: x => x.ambulance_id,
                        principalTable: "ambulances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ambulance_status_history_ambulance_id_started_at",
                table: "ambulance_status_history",
                columns: new[] { "ambulance_id", "started_at" });

            migrationBuilder.Sql("""
                INSERT INTO ambulance_status_history (id, ambulance_id, status, started_at, created_at)
                SELECT gen_random_uuid(), id, status, created_at, created_at
                FROM ambulances
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ambulance_status_history");
        }
    }
}
