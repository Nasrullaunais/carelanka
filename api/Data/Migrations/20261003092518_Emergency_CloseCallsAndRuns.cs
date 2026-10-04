using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_CloseCallsAndRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                table: "emergency_calls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome_notes",
                table: "emergency_calls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Outcome was free text and is now a fixed list: keep the old words as notes rather than lose them.
            migrationBuilder.Sql("""
                UPDATE emergency_calls
                SET outcome_notes = outcome,
                    outcome = CASE WHEN transported THEN 'transported' ELSE NULL END
                WHERE outcome IS NOT NULL;
                UPDATE emergency_calls
                SET closed_at = updated_at
                WHERE status IN ('completed', 'cancelled') AND closed_at IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "emergency_calls",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned', 'dispatch_cancelled', 'appointment_booked', 'appointment_rescheduled', 'appointment_cancelled', 'appointment_reminder', 'admission_approved', 'bed_assigned', 'discharge_ready', 'bill_raised', 'bill_settled', 'care_reply_ready', 'prescription_ready', 'prescription_delivered', 'lab_report_ready', 'ambulance_on_the_way', 'ambulance_arrived', 'cancellation_answered', 'emergency_call_cancelled', 'emergency_call_received', 'cancellation_request_waiting', 'dispatch_proposal_waiting', 'dispatch_proposal_failed', 'admission_awaiting_approval', 'care_query_flagged', 'care_reply_waiting', 'equipment_warning_raised', 'maintenance_due', 'pharmacy_stock_low', 'lab_test_requested', 'leave_requested', 'leave_approved', 'leave_rejected', 'shift_changed', 'roster_proposal_waiting')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_emergency_calls_outcome",
                table: "emergency_calls",
                sql: "outcome IN ('transported', 'treated_at_scene', 'refused_transport', 'patient_not_found', 'deceased_at_scene', 'false_alarm', 'duplicate_call', 'caller_cancelled', 'no_longer_needed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital', 'handed_over', 'closed_at_scene', 'declined', 'cancelled', 'reassigned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals",
                sql: "withdrawal_reason IN ('call_changed', 'dispatched_manually', 'call_closed', 'ambulance_no_longer_available')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emergency_calls_outcome",
                table: "emergency_calls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals");

            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "emergency_calls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.Sql("UPDATE emergency_calls SET outcome = COALESCE(outcome_notes, outcome);");

            migrationBuilder.DropColumn(
                name: "closed_at",
                table: "emergency_calls");

            migrationBuilder.DropColumn(
                name: "outcome_notes",
                table: "emergency_calls");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned', 'appointment_booked', 'appointment_rescheduled', 'appointment_cancelled', 'appointment_reminder', 'admission_approved', 'bed_assigned', 'discharge_ready', 'bill_raised', 'bill_settled', 'care_reply_ready', 'prescription_ready', 'prescription_delivered', 'lab_report_ready', 'ambulance_on_the_way', 'ambulance_arrived', 'cancellation_answered', 'emergency_call_received', 'cancellation_request_waiting', 'dispatch_proposal_waiting', 'dispatch_proposal_failed', 'admission_awaiting_approval', 'care_query_flagged', 'care_reply_waiting', 'equipment_warning_raised', 'maintenance_due', 'pharmacy_stock_low', 'lab_test_requested', 'leave_requested', 'leave_approved', 'leave_rejected', 'shift_changed', 'roster_proposal_waiting')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital', 'handed_over', 'declined', 'cancelled', 'reassigned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_withdrawal_reason",
                table: "dispatch_proposals",
                sql: "withdrawal_reason IN ('call_changed', 'dispatched_manually', 'call_closed')");
        }
    }
}
