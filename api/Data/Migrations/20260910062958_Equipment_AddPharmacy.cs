using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddPharmacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pharmacy_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    requires_prescription = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pharmacy_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    batch_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity_on_hand = table.Column<int>(type: "integer", nullable: false),
                    reorder_threshold = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pharmacy_items", x => x.id);
                    table.CheckConstraint("ck_pharmacy_items_quantity", "quantity_on_hand >= 0");
                    table.CheckConstraint("ck_pharmacy_items_reorder_threshold", "reorder_threshold >= 0");
                    table.ForeignKey(
                        name: "fk_pharmacy_items_pharmacy_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "pharmacy_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pharmacy_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    performed_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pharmacy_transactions", x => x.id);
                    table.CheckConstraint("ck_pharmacy_transactions_quantity", "quantity > 0");
                    table.CheckConstraint("ck_pharmacy_transactions_type", "type IN ('received', 'dispensed', 'adjusted', 'expired_removed')");
                    table.ForeignKey(
                        name: "fk_pharmacy_transactions_pharmacy_items_pharmacy_item_id",
                        column: x => x.pharmacy_item_id,
                        principalTable: "pharmacy_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pharmacy_categories_name",
                table: "pharmacy_categories",
                column: "name",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_pharmacy_items_category_id",
                table: "pharmacy_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_pharmacy_items_expiry_date",
                table: "pharmacy_items",
                column: "expiry_date",
                filter: "expiry_date IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_pharmacy_items_name",
                table: "pharmacy_items",
                column: "name",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_pharmacy_transactions_item_created_at",
                table: "pharmacy_transactions",
                columns: new[] { "pharmacy_item_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pharmacy_transactions");

            migrationBuilder.DropTable(
                name: "pharmacy_items");

            migrationBuilder.DropTable(
                name: "pharmacy_categories");
        }
    }
}
