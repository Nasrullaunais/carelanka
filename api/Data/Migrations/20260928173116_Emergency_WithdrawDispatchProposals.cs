using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_WithdrawDispatchProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_status",
                table: "dispatch_proposals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_workflows_status",
                table: "agent_workflows");

            migrationBuilder.AddColumn<string>(
                name: "withdrawal_reason",
                table: "dispatch_proposals",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "withdrawn_at",
                table: "dispatch_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "dispatch_proposals",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned', 'appointment_booked', 'appointment_rescheduled', 'appointment_cancelled', 'appointment_reminder', 'admission_approved', 'bed_assigned', 'discharge_ready', 'bill_raised', 'bill_settled', 'care_reply_ready', 'prescription_ready', 'prescription_delivered', 'lab_report_ready', 'ambulance_on_the_way', 'ambulance_arrived', 'cancellation_answered', 'emergency_call_received', 'cancellation_request_waiting', 'dispatch_proposal_waiting', 'dispatch_proposal_failed', 'admission_awaiting_approval', 'care_query_flagged', 'care_reply_waiting', 'equipment_warning_raised', 'maintenance_due', 'pharmacy_stock_low', 'lab_test_requested', 'leave_requested', 'leave_approved', 'leave_rejected', 'shift_changed', 'roster_proposal_waiting')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_status",
                table: "dispatch_proposals",
                sql: "status IN ('pending', 'pending_confirmation', 'pending_approval', 'approved', 'executed', 'rejected', 'failed', 'withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals",
                sql: "withdrawal_reason IN ('call_changed', 'dispatched_manually', 'call_closed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_workflows_status",
                table: "agent_workflows",
                sql: "status IN ('pending', 'pending_approval', 'auto_approved', 'approved', 'revision_requested', 'rejected', 'executed', 'failed', 'withdrawn')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_status",
                table: "dispatch_proposals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agent_workflows_status",
                table: "agent_workflows");

            migrationBuilder.DropColumn(
                name: "withdrawal_reason",
                table: "dispatch_proposals");

            migrationBuilder.DropColumn(
                name: "withdrawn_at",
                table: "dispatch_proposals");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "dispatch_proposals");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned', 'appointment_booked', 'appointment_rescheduled', 'appointment_cancelled', 'appointment_reminder', 'admission_approved', 'bed_assigned', 'discharge_ready', 'bill_raised', 'bill_settled', 'care_reply_ready', 'prescription_ready', 'prescription_delivered', 'lab_report_ready', 'ambulance_on_the_way', 'ambulance_arrived', 'cancellation_answered', 'emergency_call_received', 'cancellation_request_waiting', 'dispatch_proposal_waiting', 'admission_awaiting_approval', 'care_query_flagged', 'care_reply_waiting', 'equipment_warning_raised', 'maintenance_due', 'pharmacy_stock_low', 'lab_test_requested', 'leave_requested', 'leave_approved', 'leave_rejected', 'shift_changed', 'roster_proposal_waiting')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_status",
                table: "dispatch_proposals",
                sql: "status IN ('pending', 'pending_confirmation', 'pending_approval', 'approved', 'executed', 'rejected', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agent_workflows_status",
                table: "agent_workflows",
                sql: "status IN ('pending', 'pending_approval', 'auto_approved', 'approved', 'revision_requested', 'rejected', 'executed', 'failed')");
        }
    }
}
