using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_RemoveBedAgentWorkflowLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments");

            migrationBuilder.DropIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bed_assignments_assigned_by",
                table: "bed_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_workflows_agent_type",
                table: "agent_workflows");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_proposed_changes_type",
                table: "agent_proposed_changes");

            migrationBuilder.DropColumn(
                name: "assigned_by",
                table: "bed_assignments");

            migrationBuilder.DropColumn(
                name: "workflow_id",
                table: "bed_assignments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_workflows_agent_type",
                table: "agent_workflows",
                sql: "agent_type IN ('dispatch_routing', 'staff_allocation', 'equipment_monitoring', 'patient_care_advisory')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_proposed_changes_type",
                table: "agent_proposed_changes",
                sql: "change_type IN ('end_allocation', 'create_allocation', 'assign_bed', 'release_bed', 'create_dispatch', 'transfer_equipment', 'create_maintenance_schedule', 'create_care_recommendation')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_workflows_agent_type",
                table: "agent_workflows");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_proposed_changes_type",
                table: "agent_proposed_changes");

            migrationBuilder.AddColumn<string>(
                name: "assigned_by",
                table: "bed_assignments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "workflow_id",
                table: "bed_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments",
                column: "workflow_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bed_assignments_assigned_by",
                table: "bed_assignments",
                sql: "assigned_by IN ('agent', 'user')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_workflows_agent_type",
                table: "agent_workflows",
                sql: "agent_type IN ('dispatch_routing', 'staff_allocation', 'equipment_monitoring', 'patient_admission_bed', 'patient_care_advisory')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_proposed_changes_type",
                table: "agent_proposed_changes",
                sql: "change_type IN ('end_allocation', 'create_allocation', 'reserve_bed', 'assign_bed', 'release_bed', 'create_dispatch', 'transfer_equipment', 'create_maintenance_schedule', 'create_care_recommendation')");

            migrationBuilder.AddForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments",
                column: "workflow_id",
                principalTable: "agent_workflows",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
