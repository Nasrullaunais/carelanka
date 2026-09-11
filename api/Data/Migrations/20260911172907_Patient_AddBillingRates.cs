using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddBillingRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_wards_type",
                table: "wards");

            migrationBuilder.CreateTable(
                name: "admission_fee_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_fee_rates", x => x.id);
                    table.CheckConstraint("ck_admission_fee_rates_amount", "amount >= 0");
                    table.CheckConstraint("ck_admission_fee_rates_category", "category IN ('icu', 'hdu', 'inpatient', 'day_case', 'outpatient')");
                });

            migrationBuilder.CreateTable(
                name: "billing_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expense_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_rates", x => x.id);
                    table.CheckConstraint("ck_billing_rates_amount", "amount >= 0");
                    table.CheckConstraint("ck_billing_rates_ward_type", "ward_type IN ('icu', 'hdu', 'general', 'maternity', 'pediatric', 'isolation', 'surgical', 'emergency', 'mental_health')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_wards_type",
                table: "wards",
                sql: "ward_type IN ('icu', 'hdu', 'general', 'maternity', 'pediatric', 'isolation', 'surgical', 'emergency', 'mental_health')");

            migrationBuilder.CreateIndex(
                name: "ux_admission_fee_rates_category",
                table: "admission_fee_rates",
                column: "category",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_billing_rates_ward_expense",
                table: "billing_rates",
                columns: new[] { "ward_type", "expense_key" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_fee_rates");

            migrationBuilder.DropTable(
                name: "billing_rates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_wards_type",
                table: "wards");

            migrationBuilder.AddCheckConstraint(
                name: "ck_wards_type",
                table: "wards",
                sql: "ward_type IN ('icu', 'hdu', 'general', 'maternity', 'pediatric', 'isolation')");
        }
    }
}
