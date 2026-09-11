using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_AddPatientCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Four steps, not one. The column is NOT NULL and unique, and rows already exist:
            // the generated single AddColumn hands every one of them the same default "" and
            // then the unique index refuses to build. So add it empty, fill it in, and only
            // then tighten it.
            //
            // This is a backfill, not seed data. Existing patients have to satisfy a constraint
            // that did not exist when they were registered, and nothing outside this migration
            // knows the rule.
            migrationBuilder.AddColumn<string>(
                name: "patient_code",
                table: "patients",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // The same alphabet and shape as PatientCodes.Next(): 'P' then seven characters
            // with no 0/O and no 1/I/L, because someone reads this off a wristband.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    alphabet CONSTANT text := '23456789ABCDEFGHJKMNPQRSTUVWXYZ';
                    row_id uuid;
                    code text;
                    i int;
                BEGIN
                    FOR row_id IN SELECT id FROM patients WHERE patient_code IS NULL LOOP
                        LOOP
                            code := 'P';

                            FOR i IN 1..7 LOOP
                                code := code || substr(
                                    alphabet, 1 + floor(random() * length(alphabet))::int, 1);
                            END LOOP;

                            EXIT WHEN NOT EXISTS (
                                SELECT 1 FROM patients WHERE patient_code = code);
                        END LOOP;

                        UPDATE patients SET patient_code = code WHERE id = row_id;
                    END LOOP;
                END $$;
            ");

            // oldNullable: true is load-bearing. Without it EF compares the column against
            // itself, decides nothing changed, and emits an ALTER TYPE that is a no-op — the
            // column stays nullable in the database while the model insists it is not, and the
            // mismatch only shows up as a NullReferenceException months later.
            migrationBuilder.AlterColumn<string>(
                name: "patient_code",
                table: "patients",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_patients_patient_code",
                table: "patients",
                column: "patient_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_patients_patient_code",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "patient_code",
                table: "patients");
        }
    }
}
