using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddPharmacyBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pharmacy_items_expiry_date",
                table: "pharmacy_items");

            migrationBuilder.AddColumn<Guid>(
                name: "pharmacy_batch_id",
                table: "pharmacy_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pharmacy_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pharmacy_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_number = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity_on_hand = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pharmacy_batches", x => x.id);
                    table.CheckConstraint("ck_pharmacy_batches_quantity", "quantity_on_hand >= 0");
                    table.ForeignKey(
                        name: "fk_pharmacy_batches_pharmacy_items_pharmacy_item_id",
                        column: x => x.pharmacy_item_id,
                        principalTable: "pharmacy_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pharmacy_batches_pharmacy_item_id_expiry_date",
                table: "pharmacy_batches",
                columns: new[] { "pharmacy_item_id", "expiry_date" },
                filter: "quantity_on_hand > 0");

            migrationBuilder.CreateIndex(
                name: "ux_pharmacy_batches_item_number",
                table: "pharmacy_batches",
                columns: new[] { "pharmacy_item_id", "batch_number" },
                unique: true);

            // Stock that was already on the shelf becomes batch 1 of its medicine, carrying the
            // quantity and expiry date the item used to hold, so no stock is left belonging to no
            // delivery. Derived from the rows themselves - nothing environment-specific.
            migrationBuilder.Sql("""
                INSERT INTO pharmacy_batches
                    (id, pharmacy_item_id, batch_number, reference, expiry_date, quantity_on_hand,
                     note, created_at, updated_at)
                SELECT gen_random_uuid(), i.id, 1, i.batch_number, i.expiry_date, i.quantity_on_hand,
                       NULL, i.created_at, i.updated_at
                FROM pharmacy_items AS i
                WHERE i.quantity_on_hand > 0 OR i.expiry_date IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "batch_number",
                table: "pharmacy_items");

            migrationBuilder.DropColumn(
                name: "expiry_date",
                table: "pharmacy_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pharmacy_batches");

            migrationBuilder.DropColumn(
                name: "pharmacy_batch_id",
                table: "pharmacy_transactions");

            migrationBuilder.AddColumn<string>(
                name: "batch_number",
                table: "pharmacy_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "expiry_date",
                table: "pharmacy_items",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pharmacy_items_expiry_date",
                table: "pharmacy_items",
                column: "expiry_date",
                filter: "expiry_date IS NOT NULL");
        }
    }
}
