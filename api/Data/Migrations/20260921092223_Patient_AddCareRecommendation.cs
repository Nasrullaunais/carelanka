using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddCareRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "care_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    red_flag = table.Column<bool>(type: "boolean", nullable: false),
                    urgency_flag = table.Column<int>(type: "integer", nullable: true),
                    agent_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reviewed_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    doctor_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_care_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_care_recommendations_admissions_admission_id",
                        column: x => x.admission_id,
                        principalTable: "admissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_care_recommendations_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_care_recommendations_staff_members_reviewed_by_staff_member",
                        column: x => x.reviewed_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_care_recommendations_admission_id",
                table: "care_recommendations",
                column: "admission_id");

            migrationBuilder.CreateIndex(
                name: "ix_care_recommendations_patient_id_reported_at",
                table: "care_recommendations",
                columns: new[] { "patient_id", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "ix_care_recommendations_reviewed_by_staff_member_id",
                table: "care_recommendations",
                column: "reviewed_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_care_recommendations_status",
                table: "care_recommendations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_recommendations");
        }
    }
}
