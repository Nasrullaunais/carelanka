using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddDispatchProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dispatch_proposal_id",
                table: "dispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dispatch_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emergency_call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    call_priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_diversion = table.Column<bool>(type: "boolean", nullable: false),
                    allow_diversion = table.Column<bool>(type: "boolean", nullable: false),
                    exclude_ambulance_ids_json = table.Column<string>(type: "text", nullable: true),
                    rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proposed_ambulance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estimated_minutes_to_scene = table.Column<int>(type: "integer", nullable: true),
                    source_dispatch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_call_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_call_priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source_call_address_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    source_dispatch_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    source_call_waiting_minutes_so_far = table.Column<int>(type: "integer", nullable: true),
                    source_call_additional_wait_minutes = table.Column<int>(type: "integer", nullable: true),
                    replacement_ambulance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minutes_saved_for_this_call = table.Column<int>(type: "integer", nullable: true),
                    resulting_dispatch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pre_admission_sent_json = table.Column<string>(type: "text", nullable: true),
                    reviewed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_proposals", x => x.id);
                    table.CheckConstraint("ck_dispatch_proposals_priority", "call_priority IN ('critical', 'high', 'medium', 'low')");
                    table.CheckConstraint("ck_dispatch_proposals_status", "status IN ('pending', 'pending_confirmation', 'pending_approval', 'approved', 'executed', 'rejected', 'failed')");
                    table.ForeignKey(
                        name: "fk_dispatch_proposals_emergency_calls_emergency_call_id",
                        column: x => x.emergency_call_id,
                        principalTable: "emergency_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispatches_dispatch_proposal_id",
                table: "dispatches",
                column: "dispatch_proposal_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_proposals_status",
                table: "dispatch_proposals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_proposals_workflow_id",
                table: "dispatch_proposals",
                column: "workflow_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dispatch_proposals_open_per_call",
                table: "dispatch_proposals",
                column: "emergency_call_id",
                unique: true,
                filter: "status IN ('pending', 'pending_confirmation', 'pending_approval')");

            migrationBuilder.AddForeignKey(
                name: "fk_dispatches_dispatch_proposals_dispatch_proposal_id",
                table: "dispatches",
                column: "dispatch_proposal_id",
                principalTable: "dispatch_proposals",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dispatches_dispatch_proposals_dispatch_proposal_id",
                table: "dispatches");

            migrationBuilder.DropTable(
                name: "dispatch_proposals");

            migrationBuilder.DropIndex(
                name: "ix_dispatches_dispatch_proposal_id",
                table: "dispatches");

            migrationBuilder.DropColumn(
                name: "dispatch_proposal_id",
                table: "dispatches");
        }
    }
}
