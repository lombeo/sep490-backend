using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixPlanItemIndexConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConstructPlanItems_Index",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConstructPlanItems_Index_PlanId",
                table: "ConstructPlanItems",
                columns: new[] { "Index", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_ParentIndex_PlanId",
                table: "ConstructPlanItems",
                columns: new[] { "ParentIndex", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems",
                columns: new[] { "PlanId", "Index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex_PlanId",
                table: "ConstructPlanItems",
                columns: new[] { "ParentIndex", "PlanId" },
                principalTable: "ConstructPlanItems",
                principalColumns: new[] { "Index", "PlanId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex_PlanId",
                table: "ConstructPlanItems");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConstructPlanItems_Index_PlanId",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItems_ParentIndex_PlanId",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItems_PlanId_Index",
                table: "ConstructPlanItems");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConstructPlanItems_Index",
                table: "ConstructPlanItems",
                column: "Index");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems",
                column: "ParentIndex");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems",
                column: "ParentIndex",
                principalTable: "ConstructPlanItems",
                principalColumn: "Index",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
