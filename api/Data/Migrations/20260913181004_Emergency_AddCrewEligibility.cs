using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddCrewEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "location_updated_at",
                table: "ambulances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ambulance_crew_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ambulance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unassigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unassigned_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ambulance_crew_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ambulance_crew_assignments_ambulances_ambulance_id",
                        column: x => x.ambulance_id,
                        principalTable: "ambulances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ambulance_crew_assignments_staff_members_assigned_by_staff_",
                        column: x => x.assigned_by_staff_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ambulance_crew_assignments_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ambulance_crew_assignments_staff_members_unassigned_by_staf",
                        column: x => x.unassigned_by_staff_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ambulance_crew_assignments_assigned_by_staff_id",
                table: "ambulance_crew_assignments",
                column: "assigned_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_ambulance_crew_assignments_unassigned_by_staff_id",
                table: "ambulance_crew_assignments",
                column: "unassigned_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ux_ambulance_crew_assignments_current_ambulance_staff",
                table: "ambulance_crew_assignments",
                columns: new[] { "ambulance_id", "staff_member_id" },
                unique: true,
                filter: "unassigned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_ambulance_crew_assignments_current_staff",
                table: "ambulance_crew_assignments",
                column: "staff_member_id",
                unique: true,
                filter: "unassigned_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ambulance_crew_assignments");

            migrationBuilder.DropColumn(
                name: "location_updated_at",
                table: "ambulances");
        }
    }
}
