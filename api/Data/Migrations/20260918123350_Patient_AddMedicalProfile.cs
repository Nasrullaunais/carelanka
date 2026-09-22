using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddMedicalProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_medical_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    known_conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    allergies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    current_symptoms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recent_situation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_by_staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_medical_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_patient_medical_profiles_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_patient_medical_profiles_staff_members_updated_by_staff_mem",
                        column: x => x.updated_by_staff_member_id,
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_patient_medical_profiles_updated_by_staff_member_id",
                table: "patient_medical_profiles",
                column: "updated_by_staff_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_patient_medical_profiles_patient_id",
                table: "patient_medical_profiles",
                column: "patient_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_medical_profiles");
        }
    }
}
