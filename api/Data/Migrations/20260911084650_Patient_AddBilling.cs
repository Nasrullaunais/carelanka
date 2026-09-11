using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "occupied_at",
                table: "bed_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_number = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    settled_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement_note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bills", x => x.id);
                    table.ForeignKey(
                        name: "fk_bills_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bills_staff_members_settled_by_staff_member_id",
                        column: x => x.settled_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_line_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bed_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bill_line_items", x => x.id);
                    table.CheckConstraint("ck_bill_line_items_quantity", "quantity > 0");
                    table.CheckConstraint("ck_bill_line_items_source", "source IN ('admission_fee', 'bed_stay', 'manual')");
                    table.CheckConstraint("ck_bill_line_items_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_bill_line_items_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_line_items_bill_id",
                table: "bill_line_items",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_settled_by_staff_member_id",
                table: "bills",
                column: "settled_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_bills_admission_id",
                table: "bills",
                column: "admission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bills_bill_number",
                table: "bills",
                column: "bill_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_line_items");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropColumn(
                name: "occupied_at",
                table: "bed_assignments");
        }
    }
}
