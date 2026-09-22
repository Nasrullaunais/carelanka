using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patient_LinkBedAssignmentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments",
                column: "workflow_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments",
                column: "workflow_id",
                principalTable: "agent_workflows",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bed_assignments_agent_workflows_workflow_id",
                table: "bed_assignments");

            migrationBuilder.DropIndex(
                name: "ix_bed_assignments_workflow_id",
                table: "bed_assignments");
        }
    }
}
