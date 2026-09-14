using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <summary>
    /// Patients sign in with a username instead of a phone number.
    /// </summary>
    /// <remarks>
    /// The two <c>UPDATE</c>s are the data half of a column rename, not seed data:
    /// a new <c>NOT NULL UNIQUE</c> column cannot be added to a table that already
    /// has rows without deciding what those rows get. They derive the value from the
    /// column being dropped, so they carry no environment-specific ids.
    /// </remarks>
    public partial class Common_PatientUsernameLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "patient_accounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Existing accounts signed in with a phone number, so its digits become
            // the username and the same person still reaches the same row. The old
            // unique index on phone_number means the result is unique too.
            migrationBuilder.Sql(
                "UPDATE patient_accounts " +
                "SET username = regexp_replace(phone_number, '[^a-zA-Z0-9._-]', '', 'g');");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "patient_accounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "ux_patient_accounts_phone",
                table: "patient_accounts");

            // Both duplicated the patients table. A patient's name and contact number
            // belong on their medical record, which is where the app now collects them.
            migrationBuilder.DropColumn(
                name: "full_name",
                table: "patient_accounts");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "patient_accounts");

            migrationBuilder.CreateIndex(
                name: "ux_patient_accounts_username",
                table: "patient_accounts",
                column: "username",
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "patient_accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "patient_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // The reverse of the backfill above. A username that was never a phone
            // number comes back as itself, which is the best this direction can do.
            migrationBuilder.Sql("UPDATE patient_accounts SET phone_number = left(username, 20);");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                table: "patient_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "ux_patient_accounts_username",
                table: "patient_accounts");

            migrationBuilder.DropColumn(
                name: "username",
                table: "patient_accounts");

            migrationBuilder.CreateIndex(
                name: "ux_patient_accounts_phone",
                table: "patient_accounts",
                column: "phone_number",
                unique: true,
                filter: "is_active");
        }
    }
}
