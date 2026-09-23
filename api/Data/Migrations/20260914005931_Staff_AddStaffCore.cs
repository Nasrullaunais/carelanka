using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Staff_AddStaffCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    required_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    required_skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    headcount_needed = table.Column<int>(type: "integer", nullable: false),
                    minimum_headcount = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shifts", x => x.id);
                    table.CheckConstraint("ck_shifts_headcount", "headcount_needed > 0 AND minimum_headcount > 0 AND minimum_headcount <= headcount_needed");
                    table.CheckConstraint("ck_shifts_required_role", "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");
                    table.ForeignKey(
                        name: "fk_shifts_skills_required_skill_id",
                        column: x => x.required_skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_member_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_member_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_member_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_member_skills_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ward_staffing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    required_skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minimum_headcount = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ward_staffing_rules", x => x.id);
                    table.CheckConstraint("ck_ward_staffing_rules_required_role", "required_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");
                    table.CheckConstraint("ck_wsr_min", "minimum_headcount > 0");
                    table.ForeignKey(
                        name: "fk_ward_staffing_rules_skills_required_skill_id",
                        column: x => x.required_skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    replaced_by_allocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clocked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    clocked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    roster_proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocations", x => x.id);
                    table.CheckConstraint("ck_allocations_ended_reason", "ended_reason IS NULL OR ended_reason IN ('leave_approved', 'swapped_out', 'shift_cancelled', 'staff_deactivated', 'manual')");
                    table.CheckConstraint("ck_allocations_source", "source IN ('manual', 'agent_proposal', 'swap_request')");
                    table.CheckConstraint("ck_allocations_status", "status IN ('proposed', 'confirmed', 'released', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_allocations_allocations_replaced_by_allocation_id",
                        column: x => x.replaced_by_allocation_id,
                        principalTable: "allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocations_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocations_staff_members_created_by_staff_id",
                        column: x => x.created_by_staff_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocations_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    swap_with_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    swap_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_requests", x => x.id);
                    table.CheckConstraint("ck_leave_requests_status", "status IN ('pending', 'approved', 'rejected', 'withdrawn')");
                    table.CheckConstraint("ck_leave_requests_type", "type IN ('annual', 'sick', 'emergency', 'shift_swap')");
                    table.ForeignKey(
                        name: "fk_leave_requests_shifts_swap_shift_id",
                        column: x => x.swap_shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_staff_members_reviewed_by_staff_member_id",
                        column: x => x.reviewed_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leave_requests_staff_members_swap_with_staff_member_id",
                        column: x => x.swap_with_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocations_created_by_staff_id",
                table: "allocations",
                column: "created_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_replaced_by_allocation_id",
                table: "allocations",
                column: "replaced_by_allocation_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_shift_id",
                table: "allocations",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_staff_id",
                table: "allocations",
                column: "staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_allocations_confirmed",
                table: "allocations",
                columns: new[] { "shift_id", "staff_member_id" },
                unique: true,
                filter: "status = 'confirmed'");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_reviewed_by_staff_member_id",
                table: "leave_requests",
                column: "reviewed_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_staff_id",
                table: "leave_requests",
                column: "staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_status_start",
                table: "leave_requests",
                columns: new[] { "status", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_swap_shift_id",
                table: "leave_requests",
                column: "swap_shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_swap_with_staff_member_id",
                table: "leave_requests",
                column: "swap_with_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_date",
                table: "shifts",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_required_skill_id",
                table: "shifts",
                column: "required_skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_shifts_ward_date",
                table: "shifts",
                columns: new[] { "ward_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ux_skills_name",
                table: "skills",
                column: "name",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_staff_member_skills_skill_id",
                table: "staff_member_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ux_staff_member_skills_staff_skill",
                table: "staff_member_skills",
                columns: new[] { "staff_member_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ward_staffing_rules_required_skill_id",
                table: "ward_staffing_rules",
                column: "required_skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_ward_staffing_rules_ward_id",
                table: "ward_staffing_rules",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "ux_ward_staffing_rules_ward_role_skill",
                table: "ward_staffing_rules",
                columns: new[] { "ward_id", "required_role", "required_skill_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocations");

            migrationBuilder.DropTable(
                name: "leave_requests");

            migrationBuilder.DropTable(
                name: "staff_member_skills");

            migrationBuilder.DropTable(
                name: "ward_staffing_rules");

            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
