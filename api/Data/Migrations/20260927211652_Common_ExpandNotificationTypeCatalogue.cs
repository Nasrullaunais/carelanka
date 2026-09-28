using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Common_ExpandNotificationTypeCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned', 'appointment_booked', 'appointment_rescheduled', 'appointment_cancelled', 'appointment_reminder', 'admission_approved', 'bed_assigned', 'discharge_ready', 'bill_raised', 'bill_settled', 'care_reply_ready', 'prescription_ready', 'prescription_delivered', 'lab_report_ready', 'ambulance_on_the_way', 'ambulance_arrived', 'cancellation_answered', 'emergency_call_received', 'cancellation_request_waiting', 'dispatch_proposal_waiting', 'admission_awaiting_approval', 'care_query_flagged', 'care_reply_waiting', 'equipment_warning_raised', 'maintenance_due', 'pharmacy_stock_low', 'lab_test_requested', 'leave_requested', 'leave_approved', 'leave_rejected', 'shift_changed', 'roster_proposal_waiting')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned')");
        }
    }
}
