using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_SimplifyDischargeChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_discharge_checklist_items_item_type",
                table: "discharge_checklist_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bed_assignments_release_reason",
                table: "bed_assignments");

            // The one place this component writes data from a migration, and it is not a
            // seed. A CHECK cannot be added to a table that already breaks it, so these rows
            // have to go before the constraint below can exist at all - the DELETE is part of
            // the schema change rather than a fact about any hospital.
            //
            // Nothing is lost that is worth keeping. Each of these boxes recorded that
            // something had been given to or arranged for the patient, and that is what the
            // bill records, with a price against it.
            migrationBuilder.Sql(
                """
                DELETE FROM discharge_checklist_items
                WHERE item_type IN ('medication_issued', 'follow_up_recorded', 'transport_arranged');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_discharge_checklist_items_item_type",
                table: "discharge_checklist_items",
                sql: "item_type IN ('clinical_clearance', 'billing_settled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bed_assignments_release_reason",
                table: "bed_assignments",
                sql: "release_reason IS NULL OR release_reason IN ('discharged', 'hold_expired', 'cancelled', 'transferred', 'rejected', 'corrected')");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Puts the vocabulary back but not the rows. A deleted tick cannot be un-deleted -
        /// nothing records who ticked it or when any more - and inventing them on the way back
        /// would be worse than the gap. A rolled-back database has the three boxes available
        /// and unticked, which is what a fresh checklist looks like anyway.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_discharge_checklist_items_item_type",
                table: "discharge_checklist_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bed_assignments_release_reason",
                table: "bed_assignments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_discharge_checklist_items_item_type",
                table: "discharge_checklist_items",
                sql: "item_type IN ('clinical_clearance', 'medication_issued', 'billing_settled', 'follow_up_recorded', 'transport_arranged')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bed_assignments_release_reason",
                table: "bed_assignments",
                sql: "release_reason IS NULL OR release_reason IN ('discharged', 'hold_expired', 'cancelled', 'transferred', 'rejected')");
        }
    }
}
