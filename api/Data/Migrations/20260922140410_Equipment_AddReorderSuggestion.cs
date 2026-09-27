using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment_AddReorderSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reorder_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pharmacy_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_threshold = table.Column<int>(type: "integer", nullable: false),
                    current_quantity_on_hand = table.Column<int>(type: "integer", nullable: false),
                    suggested_threshold = table.Column<int>(type: "integer", nullable: true),
                    reasoning = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reorder_suggestions", x => x.id);
                    table.CheckConstraint("ck_reorder_suggestions_source", "source IN ('model', 'model_unavailable', 'model_rejected')");
                    table.ForeignKey(
                        name: "fk_reorder_suggestions_pharmacy_items_pharmacy_item_id",
                        column: x => x.pharmacy_item_id,
                        principalTable: "pharmacy_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reorder_suggestions_pharmacy_item_id",
                table: "reorder_suggestions",
                column: "pharmacy_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reorder_suggestions");
        }
    }
}
