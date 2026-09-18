using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddBedAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_workflow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    objective = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    completed_steps = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    tool_results = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    validation_results = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    errors = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    required_approver_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reviewed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    final_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_workflows", x => x.id);
                    table.CheckConstraint("ck_agent_workflows_agent_type", "agent_type IN ('coordinator', 'dispatch_routing', 'patient_admission_bed', 'staff_allocation', 'equipment_monitoring')");
                    table.CheckConstraint("ck_agent_workflows_objective", "objective IN ('emergency_response', 'assign_bed', 'fill_roster_gap', 'monitor_stock_and_maintenance', 'check_ward_readiness')");
                    table.CheckConstraint("ck_agent_workflows_required_approver_role", "required_approver_role IS NULL OR required_approver_role IN ('ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'duty_manager', 'hospital_administrator', 'equipment_manager')");
                    table.CheckConstraint("ck_agent_workflows_status", "status IN ('running', 'awaiting_approval', 'approved', 'rejected', 'revision_requested', 'completed', 'failed')");
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
                name: "bed_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_identifier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    blocker_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    blocker_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    validation_passed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bed_suggestions", x => x.id);
                    table.CheckConstraint("ck_bed_suggestions_blocker_code", "blocker_code IS NULL OR blocker_code IN ('downgrade_needed', 'upgrade_only', 'ward_full', 'gender_policy', 'needs_isolation', 'pediatric_only', 'no_bed_required', 'no_such_patient', 'no_open_admission', 'agent_failed')");
                    table.CheckConstraint("ck_bed_suggestions_outcome", "outcome IS NULL OR outcome IN ('proposed', 'proposed_with_downgrade', 'needs_duty_manager', 'no_bed_available', 'visit_needs_no_bed', 'patient_not_found', 'failed')");
                    table.ForeignKey(
                        name: "fk_bed_suggestions_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bed_suggestions_agent_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "agent_workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bed_suggestions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bed_suggestion_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_suggestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    is_downgrade = table.Column<bool>(type: "boolean", nullable: false),
                    requires_duty_manager = table.Column<bool>(type: "boolean", nullable: false),
                    rules_satisfied = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bed_suggestion_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_bed_suggestion_candidates_bed_suggestions_bed_suggestion_id",
                        column: x => x.bed_suggestion_id,
                        principalTable: "bed_suggestions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_workflows_correlation_id",
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

            migrationBuilder.CreateIndex(
                name: "ux_bed_suggestion_candidates_rank",
                table: "bed_suggestion_candidates",
                columns: new[] { "bed_suggestion_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bed_suggestions_admission_id",
                table: "bed_suggestions",
                column: "admission_id");

            migrationBuilder.CreateIndex(
                name: "ix_bed_suggestions_patient_id",
                table: "bed_suggestions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_bed_suggestions_workflow_id",
                table: "bed_suggestions",
                column: "workflow_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments",
                column: "workflow_id",
                principalTable: "agent_workflows",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments");

            migrationBuilder.DropTable(
                name: "bed_suggestion_candidates");

            migrationBuilder.DropTable(
                name: "bed_suggestions");

            migrationBuilder.DropTable(
                name: "agent_workflows");

            migrationBuilder.DropIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments");
        }
    }
}
