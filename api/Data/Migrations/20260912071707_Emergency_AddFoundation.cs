using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ambulances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    current_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    out_of_service_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ambulances", x => x.id);
                    table.CheckConstraint("ck_ambulances_latitude", "current_latitude IS NULL OR current_latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_ambulances_longitude", "current_longitude IS NULL OR current_longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_ambulances_status", "status IN ('available', 'dispatched', 'en_route', 'at_scene', 'transporting', 'out_of_service')");
                });

            migrationBuilder.CreateTable(
                name: "emergency_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    caller_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_is_caller = table.Column<bool>(type: "boolean", nullable: false),
                    caller_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    caller_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    address_label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    outcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    transported = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emergency_calls", x => x.id);
                    table.CheckConstraint("ck_emergency_calls_latitude", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_emergency_calls_longitude", "longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_emergency_calls_priority", "priority IN ('critical', 'high', 'medium', 'low')");
                    table.CheckConstraint("ck_emergency_calls_status", "status IN ('received', 'dispatched', 'en_route', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_emergency_calls_patient_accounts_caller_user_id",
                        column: x => x.caller_user_id,
                        principalTable: "patient_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_emergency_calls_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emergency_call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ambulance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_ward_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    superseded_by_dispatch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatches", x => x.id);
                    table.CheckConstraint("ck_dispatches_status", "status IN ('assigned', 'en_route', 'completed', 'cancelled', 'reassigned')");
                    table.ForeignKey(
                        name: "fk_dispatches_ambulances_ambulance_id",
                        column: x => x.ambulance_id,
                        principalTable: "ambulances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatches_dispatches_superseded_by_dispatch_id",
                        column: x => x.superseded_by_dispatch_id,
                        principalTable: "dispatches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatches_emergency_calls_emergency_call_id",
                        column: x => x.emergency_call_id,
                        principalTable: "emergency_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatches_wards_destination_ward_id",
                        column: x => x.destination_ward_id,
                        principalTable: "wards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_crew",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_crew", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispatch_crew_dispatches_dispatch_id",
                        column: x => x.dispatch_id,
                        principalTable: "dispatches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dispatch_crew_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    origin_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    destination_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    destination_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    planned_distance_km = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    planned_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    departed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    arrived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    maps_api_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_logs", x => x.id);
                    table.CheckConstraint("ck_route_logs_destination_latitude", "destination_latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_route_logs_destination_longitude", "destination_longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_route_logs_distance", "planned_distance_km >= 0");
                    table.CheckConstraint("ck_route_logs_duration", "planned_duration_minutes >= 0");
                    table.CheckConstraint("ck_route_logs_origin_latitude", "origin_latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_route_logs_origin_longitude", "origin_longitude BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "fk_route_logs_dispatches_dispatch_id",
                        column: x => x.dispatch_id,
                        principalTable: "dispatches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ambulances_status",
                table: "ambulances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_ambulances_registration_number",
                table: "ambulances",
                column: "registration_number",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_crew_dispatch_id_staff_member_id",
                table: "dispatch_crew",
                columns: new[] { "dispatch_id", "staff_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_crew_staff_member_id",
                table: "dispatch_crew",
                column: "staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatches_destination_ward_id",
                table: "dispatches",
                column: "destination_ward_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatches_emergency_call_id",
                table: "dispatches",
                column: "emergency_call_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatches_status_dispatched_at",
                table: "dispatches",
                columns: new[] { "status", "dispatched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dispatches_superseded_by_dispatch_id",
                table: "dispatches",
                column: "superseded_by_dispatch_id");

            migrationBuilder.CreateIndex(
                name: "ux_dispatches_active_ambulance",
                table: "dispatches",
                column: "ambulance_id",
                unique: true,
                filter: "status IN ('assigned', 'en_route')");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_calls_caller_user_id",
                table: "emergency_calls",
                column: "caller_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_calls_patient_id",
                table: "emergency_calls",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_emergency_calls_priority_created_at",
                table: "emergency_calls",
                columns: new[] { "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_emergency_calls_status_created_at",
                table: "emergency_calls",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_route_logs_dispatch_id",
                table: "route_logs",
                column: "dispatch_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_crew");

            migrationBuilder.DropTable(
                name: "route_logs");

            migrationBuilder.DropTable(
                name: "dispatches");

            migrationBuilder.DropTable(
                name: "ambulances");

            migrationBuilder.DropTable(
                name: "emergency_calls");
        }
    }
}
