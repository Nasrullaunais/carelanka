using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Common_AddAgentWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_workflow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    objective = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    completed_steps = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    tool_results = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    validation_results = table.Column<string>(type: "jsonb", nullable: true),
                    errors = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    required_approver_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    reviewed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    final_outcome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_workflows", x => x.id);
                    table.CheckConstraint("ck_agent_workflows_agent_type", "agent_type IN ('dispatch_routing', 'staff_allocation', 'equipment_monitoring', 'patient_admission_bed', 'patient_care_advisory')");
                    table.CheckConstraint("ck_agent_workflows_attempts", "attempt_count >= 0");
                    table.CheckConstraint("ck_agent_workflows_status", "status IN ('pending', 'pending_approval', 'auto_approved', 'approved', 'revision_requested', 'rejected', 'executed', 'failed')");
                    table.ForeignKey(
                        name: "fk_agent_workflows_agent_workflows_parent_workflow_id",
                        column: x => x.parent_workflow_id,
                        principalTable: "agent_workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_workflows_staff_members_reviewed_by_staff_member_id",
                        column: x => x.reviewed_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_proposed_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    change_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_bed_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_ward_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    validation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validation_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_proposed_changes", x => x.id);
                    table.CheckConstraint("ck_agent_proposed_changes_sequence", "sequence >= 1");
                    table.CheckConstraint("ck_agent_proposed_changes_type", "change_type IN ('end_allocation', 'create_allocation', 'reserve_bed', 'assign_bed', 'release_bed', 'create_dispatch', 'transfer_equipment', 'create_maintenance_schedule', 'create_care_recommendation')");
                    table.CheckConstraint("ck_agent_proposed_changes_validation", "validation_status IN ('pending', 'passed', 'failed')");
                    table.ForeignKey(
                        name: "fk_agent_proposed_changes_agent_workflows_agent_workflow_id",
                        column: x => x.agent_workflow_id,
                        principalTable: "agent_workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_proposed_changes_beds_proposed_bed_id",
                        column: x => x.proposed_bed_id,
                        principalTable: "beds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_proposed_changes_staff_members_proposed_staff_member_",
                        column: x => x.proposed_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_proposed_changes_wards_proposed_ward_id",
                        column: x => x.proposed_ward_id,
                        principalTable: "wards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_proposed_changes_proposed_bed_id",
                table: "agent_proposed_changes",
                column: "proposed_bed_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_proposed_changes_proposed_staff_member_id",
                table: "agent_proposed_changes",
                column: "proposed_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_proposed_changes_proposed_ward_id",
                table: "agent_proposed_changes",
                column: "proposed_ward_id");

            migrationBuilder.CreateIndex(
                name: "ux_agent_proposed_changes_sequence",
                table: "agent_proposed_changes",
                columns: new[] { "agent_workflow_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_agent_status",
                table: "agent_workflows",
                columns: new[] { "agent_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_correlation",
                table: "agent_workflows",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_entity",
                table: "agent_workflows",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_parent_workflow_id",
                table: "agent_workflows",
                column: "parent_workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_reviewed_by_staff_member_id",
                table: "agent_workflows",
                column: "reviewed_by_staff_member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_proposed_changes");

            migrationBuilder.DropTable(
                name: "agent_workflows");
        }
    }
}
