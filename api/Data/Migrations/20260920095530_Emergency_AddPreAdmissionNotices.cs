using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddPreAdmissionNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pre_admission_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emergency_call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pre_admission_notices", x => x.id);
                    table.CheckConstraint("ck_pre_admission_notices_attempts", "attempt_count >= 0");
                    table.CheckConstraint("ck_pre_admission_notices_status", "status IN ('queued', 'sent', 'failed')");
                    table.ForeignKey(
                        name: "fk_pre_admission_notices_dispatches_dispatch_id",
                        column: x => x.dispatch_id,
                        principalTable: "dispatches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pre_admission_notices_emergency_calls_emergency_call_id",
                        column: x => x.emergency_call_id,
                        principalTable: "emergency_calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pre_admission_notices_dispatch_id",
                table: "pre_admission_notices",
                column: "dispatch_id");

            migrationBuilder.CreateIndex(
                name: "ix_pre_admission_notices_due",
                table: "pre_admission_notices",
                column: "next_attempt_at",
                filter: "status = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ux_pre_admission_notices_call",
                table: "pre_admission_notices",
                column: "emergency_call_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pre_admission_notices");
        }
    }
}
