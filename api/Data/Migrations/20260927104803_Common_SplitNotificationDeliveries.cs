using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Common_SplitNotificationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move every existing row's delivery fields into notification_deliveries before
            // the columns they came from are dropped, so no in-flight push loses its state.
            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_notification_deliveries", x => x.id);
                    table.CheckConstraint("ck_notification_deliveries_channel", "channel IN ('in_app', 'push', 'sms')");
                    table.CheckConstraint("ck_notification_deliveries_status", "status IN ('queued', 'sent', 'failed')");
                    table.ForeignKey(
                        name: "fk_notification_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO notification_deliveries
                    (id, notification_id, channel, status, attempt_count, next_attempt_at, sent_at, failure_reason, created_at, updated_at)
                SELECT gen_random_uuid(), id, channel, status, attempt_count, next_attempt_at, sent_at, failure_reason, created_at, updated_at
                FROM notifications;
                """);

            migrationBuilder.DropIndex(
                name: "ix_notifications_due",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_channel",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_status",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "sent_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "status",
                table: "notifications");

            migrationBuilder.RenameIndex(
                name: "ix_notifications_recipient",
                table: "notifications",
                newName: "ix_notifications_staff_recipient");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_staff_member_id",
                table: "notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "recipient_patient_account_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            // Every row up to now came from the one existing caller, DispatchService, so
            // that is the only value old rows can correctly be backfilled with.
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "dispatch_assigned");

            migrationBuilder.AlterColumn<Guid>(
                name: "staff_member_id",
                table: "device_tokens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "patient_account_id",
                table: "device_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_patient_recipient",
                table: "notifications",
                columns: new[] { "recipient_patient_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_patient_unread",
                table: "notifications",
                column: "recipient_patient_account_id",
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_staff_unread",
                table: "notifications",
                column: "recipient_staff_member_id",
                filter: "read_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_one_recipient",
                table: "notifications",
                sql: "num_nonnulls(recipient_staff_member_id, recipient_patient_account_id) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('dispatch_assigned')");

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_patient_account",
                table: "device_tokens",
                column: "patient_account_id",
                filter: "revoked_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_device_tokens_one_owner",
                table: "device_tokens",
                sql: "num_nonnulls(staff_member_id, patient_account_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_due",
                table: "notification_deliveries",
                column: "next_attempt_at",
                filter: "status = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_notification_id",
                table: "notification_deliveries",
                column: "notification_id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_tokens_patient_accounts_patient_account_id",
                table: "device_tokens",
                column: "patient_account_id",
                principalTable: "patient_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_patient_accounts_recipient_patient_account_id",
                table: "notifications",
                column: "recipient_patient_account_id",
                principalTable: "patient_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_tokens_patient_accounts_patient_account_id",
                table: "device_tokens");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_patient_accounts_recipient_patient_account_id",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropIndex(
                name: "ix_notifications_patient_recipient",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_patient_unread",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_staff_unread",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_one_recipient",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_device_tokens_patient_account",
                table: "device_tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_device_tokens_one_owner",
                table: "device_tokens");

            migrationBuilder.DropColumn(
                name: "recipient_patient_account_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "type",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "patient_account_id",
                table: "device_tokens");

            migrationBuilder.RenameIndex(
                name: "ix_notifications_staff_recipient",
                table: "notifications",
                newName: "ix_notifications_recipient");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_staff_member_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "channel",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sent_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "staff_member_id",
                table: "device_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_due",
                table: "notifications",
                column: "next_attempt_at",
                filter: "status = 'queued'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_channel",
                table: "notifications",
                sql: "channel IN ('in_app', 'push', 'sms')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_status",
                table: "notifications",
                sql: "status IN ('queued', 'sent', 'failed')");
        }
    }
}
