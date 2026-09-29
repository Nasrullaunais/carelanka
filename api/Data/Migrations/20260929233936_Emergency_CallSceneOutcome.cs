using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_CallSceneOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pre_admission_notices_due",
                table: "pre_admission_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pre_admission_notices_status",
                table: "pre_admission_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admissions_cancel_reason",
                table: "admissions");

            migrationBuilder.RenameColumn(
                name: "outcome",
                table: "emergency_calls",
                newName: "scene_outcome_notes");

            migrationBuilder.AddColumn<string>(
                name: "withdrawal_reason",
                table: "pre_admission_notices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "pre_admission_notices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "scene_outcome",
                table: "emergency_calls",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pre_admission_notices_due",
                table: "pre_admission_notices",
                column: "next_attempt_at",
                filter: "status IN ('queued', 'withdrawing')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pre_admission_notices_status",
                table: "pre_admission_notices",
                sql: "status IN ('queued', 'sent', 'failed', 'withdrawn', 'withdrawing', 'withdrawal_failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pre_admission_notices_withdrawal_reason",
                table: "pre_admission_notices",
                sql: "withdrawal_reason IS NULL OR withdrawal_reason IN ('diverted_to_other_hospital', 'false_alarm', 'died_en_route', 'patient_refused', 'no_show', 'treated_at_scene', 'died_at_scene', 'call_cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_emergency_calls_scene_outcome",
                table: "emergency_calls",
                sql: "scene_outcome IS NULL OR scene_outcome IN ('treated_at_scene', 'patient_refused', 'patient_not_found', 'false_alarm', 'patient_deceased')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital', 'handed_over', 'declined', 'cancelled', 'reassigned', 'ended_at_scene')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admissions_cancel_reason",
                table: "admissions",
                sql: "cancel_reason IS NULL OR cancel_reason IN ('diverted_to_other_hospital', 'false_alarm', 'died_en_route', 'patient_refused', 'no_show', 'treated_at_scene', 'died_at_scene', 'call_cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pre_admission_notices_due",
                table: "pre_admission_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pre_admission_notices_status",
                table: "pre_admission_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pre_admission_notices_withdrawal_reason",
                table: "pre_admission_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emergency_calls_scene_outcome",
                table: "emergency_calls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admissions_cancel_reason",
                table: "admissions");

            migrationBuilder.DropColumn(
                name: "withdrawal_reason",
                table: "pre_admission_notices");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "pre_admission_notices");

            migrationBuilder.DropColumn(
                name: "scene_outcome",
                table: "emergency_calls");

            migrationBuilder.RenameColumn(
                name: "scene_outcome_notes",
                table: "emergency_calls",
                newName: "outcome");

            migrationBuilder.CreateIndex(
                name: "ix_pre_admission_notices_due",
                table: "pre_admission_notices",
                column: "next_attempt_at",
                filter: "status = 'queued'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pre_admission_notices_status",
                table: "pre_admission_notices",
                sql: "status IN ('queued', 'sent', 'failed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatches_status",
                table: "dispatches",
                sql: "status IN ('assigned', 'acknowledged', 'en_route_to_scene', 'at_scene', 'transporting_to_hospital', 'handed_over', 'declined', 'cancelled', 'reassigned')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admissions_cancel_reason",
                table: "admissions",
                sql: "cancel_reason IS NULL OR cancel_reason IN ('diverted_to_other_hospital', 'false_alarm', 'died_en_route', 'patient_refused', 'no_show')");
        }
    }
}
