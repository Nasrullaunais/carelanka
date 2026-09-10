using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performed_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_schedules", x => x.id);
                    table.CheckConstraint("ck_maintenance_schedules_asset_type", "asset_type IN ('equipment_item', 'bed')");
                    table.CheckConstraint("ck_maintenance_schedules_created_by", "created_by IN ('agent', 'user')");
                    table.CheckConstraint("ck_maintenance_schedules_schedule_type", "schedule_type IN ('routine_service', 'calibration', 'repair')");
                    table.CheckConstraint("ck_maintenance_schedules_status", "status IN ('scheduled', 'in_progress', 'completed', 'overdue', 'cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "warnings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recommended_action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    raised_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warnings", x => x.id);
                    table.CheckConstraint("ck_warnings_raised_by", "raised_by IN ('agent', 'user')");
                    table.CheckConstraint("ck_warnings_related_entity_type", "related_entity_type IN ('pharmacy_item', 'equipment_item', 'bed')");
                    table.CheckConstraint("ck_warnings_severity", "severity IN ('low', 'medium', 'high', 'critical')");
                    table.CheckConstraint("ck_warnings_status", "status IN ('open', 'acknowledged', 'action_taken', 'dismissed')");
                    table.CheckConstraint("ck_warnings_type", "type IN ('low_stock', 'medicine_expiring', 'maintenance_overdue', 'equipment_faulty')");
                });

            migrationBuilder.CreateTable(
                name: "equipment_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_admission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    next_maintenance_due = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_items", x => x.id);
                    table.CheckConstraint("ck_equipment_items_status", "status IN ('available', 'assigned', 'maintenance', 'retired')");
                    table.ForeignKey(
                        name: "fk_equipment_items_equipment_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "equipment_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_equipment_categories_name",
                table: "equipment_categories",
                column: "name",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_items_category_id",
                table: "equipment_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_items_next_maintenance_due",
                table: "equipment_items",
                column: "next_maintenance_due",
                filter: "status <> 'retired'");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_items_status",
                table: "equipment_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_items_ward_id",
                table: "equipment_items",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_items_asset_tag",
                table: "equipment_items",
                column: "asset_tag",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_items_serial_number",
                table: "equipment_items",
                column: "serial_number",
                unique: true,
                filter: "is_active AND serial_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_schedules_asset_type_asset_id",
                table: "maintenance_schedules",
                columns: new[] { "asset_type", "asset_id" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_schedules_scheduled_date",
                table: "maintenance_schedules",
                column: "scheduled_date",
                filter: "status IN ('scheduled', 'in_progress')");

            migrationBuilder.CreateIndex(
                name: "ix_warnings_related_entity_type_related_entity_id",
                table: "warnings",
                columns: new[] { "related_entity_type", "related_entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_warnings_status",
                table: "warnings",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_items");

            migrationBuilder.DropTable(
                name: "maintenance_schedules");

            migrationBuilder.DropTable(
                name: "warnings");

            migrationBuilder.DropTable(
                name: "equipment_categories");
        }
    }
}
