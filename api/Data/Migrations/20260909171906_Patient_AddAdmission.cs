using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    temp_reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                    table.CheckConstraint("ck_patients_gender", "gender IN ('male', 'female', 'other', 'unknown')");
                    table.CheckConstraint("ck_patients_identifier", "nic IS NOT NULL OR phone IS NOT NULL OR temp_reference IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_patients_patient_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "patient_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    urgency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_infectious = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    category_set_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatch_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_arrival_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    discharged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    missing_fields = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    details_complete = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "cardinality(missing_fields) = 0", stored: true),
                    details_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admissions", x => x.id);
                    table.CheckConstraint("ck_admissions_cancel_reason", "cancel_reason IS NULL OR cancel_reason IN ('diverted_to_other_hospital', 'false_alarm', 'died_en_route', 'patient_refused', 'no_show')");
                    table.CheckConstraint("ck_admissions_category", "category IN ('icu', 'hdu', 'inpatient', 'day_case', 'outpatient')");
                    table.CheckConstraint("ck_admissions_source", "source IN ('emergency', 'walk_in', 'pre_registered')");
                    table.CheckConstraint("ck_admissions_status", "status IN ('awaiting_bed', 'awaiting_approval', 'bed_reserved', 'admitted', 'ready_for_discharge', 'discharged', 'cancelled')");
                    table.CheckConstraint("ck_admissions_urgency", "urgency IN ('routine', 'urgent', 'emergency')");
                    table.ForeignKey(
                        name: "fk_admissions_patient_accounts_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "patient_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admissions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admissions_staff_members_category_set_by_staff_member_id",
                        column: x => x.category_set_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    booked_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.CheckConstraint("ck_appointments_status", "status IN ('scheduled', 'checked_in', 'completed', 'cancelled', 'no_show')");
                    table.ForeignKey(
                        name: "fk_appointments_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_staff_members_booked_by_staff_member_id",
                        column: x => x.booked_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bed_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reserved_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_downgrade = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    approved_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bed_assignments", x => x.id);
                    table.CheckConstraint("ck_bed_assignments_assigned_by", "assigned_by IN ('agent', 'user')");
                    table.CheckConstraint("ck_bed_assignments_release_reason", "release_reason IS NULL OR release_reason IN ('discharged', 'hold_expired', 'cancelled', 'transferred', 'rejected')");
                    table.CheckConstraint("ck_bed_assignments_status", "status IN ('reserved', 'occupied', 'released')");
                    table.ForeignKey(
                        name: "fk_bed_assignments_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bed_assignments_staff_members_approved_by_staff_member_id",
                        column: x => x.approved_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discharges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flagged_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    flagged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    summary_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discharges", x => x.id);
                    table.CheckConstraint("ck_discharges_flagged_by", "flagged_by IN ('agent', 'user')");
                    table.ForeignKey(
                        name: "fk_discharges_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discharges_staff_members_confirmed_by_staff_member_id",
                        column: x => x.confirmed_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discharge_checklist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discharge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ticked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ticked_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discharge_checklist_items", x => x.id);
                    table.CheckConstraint("ck_discharge_checklist_items_item_type", "item_type IN ('clinical_clearance', 'medication_issued', 'billing_settled', 'follow_up_recorded', 'transport_arranged')");
                    table.ForeignKey(
                        name: "fk_discharge_checklist_items_discharges_discharge_id",
                        column: x => x.discharge_id,
                        principalTable: "discharges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_discharge_checklist_items_staff_members_ticked_by_staff_mem",
                        column: x => x.ticked_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admissions_category_set_by_staff_member_id",
                table: "admissions",
                column: "category_set_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_admissions_dispatch_id",
                table: "admissions",
                column: "dispatch_id",
                filter: "dispatch_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_admissions_patient_id",
                table: "admissions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_admissions_reported_by_user_id",
                table: "admissions",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_admissions_status_expected_arrival_at",
                table: "admissions",
                columns: new[] { "status", "expected_arrival_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_admission_id",
                table: "appointments",
                column: "admission_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_booked_by_staff_member_id",
                table: "appointments",
                column: "booked_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id",
                table: "appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_scheduled_at_status",
                table: "appointments",
                columns: new[] { "scheduled_at", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bed_assignments_approved_by_staff_member_id",
                table: "bed_assignments",
                column: "approved_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_bed_assignments_reserved_until",
                table: "bed_assignments",
                column: "reserved_until",
                filter: "status = 'reserved'");

            migrationBuilder.CreateIndex(
                name: "ux_bed_assignments_live_admission",
                table: "bed_assignments",
                column: "admission_id",
                unique: true,
                filter: "status IN ('reserved', 'occupied')");

            migrationBuilder.CreateIndex(
                name: "ux_bed_assignments_live_bed",
                table: "bed_assignments",
                column: "bed_id",
                unique: true,
                filter: "status IN ('reserved', 'occupied')");

            migrationBuilder.CreateIndex(
                name: "ix_discharge_checklist_items_ticked_by_staff_member_id",
                table: "discharge_checklist_items",
                column: "ticked_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_discharge_checklist_items_discharge_item_type",
                table: "discharge_checklist_items",
                columns: new[] { "discharge_id", "item_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_discharges_confirmed_by_staff_member_id",
                table: "discharges",
                column: "confirmed_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_discharges_admission_id",
                table: "discharges",
                column: "admission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_patients_nic",
                table: "patients",
                column: "nic",
                unique: true,
                filter: "nic IS NOT NULL AND is_active");

            migrationBuilder.CreateIndex(
                name: "ux_patients_temp_reference",
                table: "patients",
                column: "temp_reference",
                unique: true,
                filter: "temp_reference IS NOT NULL AND is_active");

            migrationBuilder.CreateIndex(
                name: "ux_patients_user_account_id",
                table: "patients",
                column: "user_account_id",
                unique: true,
                filter: "user_account_id IS NOT NULL AND is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "bed_assignments");

            migrationBuilder.DropTable(
                name: "discharge_checklist_items");

            migrationBuilder.DropTable(
                name: "discharges");

            migrationBuilder.DropTable(
                name: "admissions");

            migrationBuilder.DropTable(
                name: "patients");
        }
    }
}
