using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLanka.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_AddDispatchRecommendationSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recommendation_note",
                table: "dispatch_proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recommendation_source",
                table: "dispatch_proposals",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_proposals_recommendation_source",
                table: "dispatch_proposals",
                sql: "recommendation_source IN ('model', 'model_unavailable', 'model_rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_proposals_recommendation_source",
                table: "dispatch_proposals");

            migrationBuilder.DropColumn(
                name: "recommendation_note",
                table: "dispatch_proposals");

            migrationBuilder.DropColumn(
                name: "recommendation_source",
                table: "dispatch_proposals");
        }
    }
}
