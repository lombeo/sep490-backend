using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class fixuniquefield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceMobilizationReqs_RequestCode",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_RequestCode",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_RequestCode",
                table: "ResourceMobilizationReqs",
                column: "RequestCode",
                unique: true,
                filter: "\"Deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_RequestCode",
                table: "ResourceAllocationReqs",
                column: "RequestCode",
                unique: true,
                filter: "\"Deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems",
                columns: new[] { "PlanId", "Index" },
                unique: true,
                filter: "\"Deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager",
                unique: true,
                filter: "\"Deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceMobilizationReqs_RequestCode",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_RequestCode",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_RequestCode",
                table: "ResourceMobilizationReqs",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_RequestCode",
                table: "ResourceAllocationReqs",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems",
                columns: new[] { "PlanId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager",
                unique: true);
        }
    }
}
