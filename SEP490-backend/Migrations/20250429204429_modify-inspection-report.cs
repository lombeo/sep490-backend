using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class modifyinspectionreport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReports_ConstructionPlans_PlanId",
                table: "InspectionReports");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReports_ConstructionProgresses_ProgressId",
                table: "InspectionReports");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReports_Projects_ProjectId",
                table: "InspectionReports");

            migrationBuilder.DropIndex(
                name: "IX_InspectionReports_PlanId",
                table: "InspectionReports");

            migrationBuilder.DropIndex(
                name: "IX_InspectionReports_ProgressId",
                table: "InspectionReports");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "InspectionReports");

            migrationBuilder.DropColumn(
                name: "ProgressId",
                table: "InspectionReports");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "InspectionReports",
                newName: "ConstructionProgressItemId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionReports_ProjectId",
                table: "InspectionReports",
                newName: "IX_InspectionReports_ConstructionProgressItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReports_ConstructionProgressItems_ConstructionPro~",
                table: "InspectionReports",
                column: "ConstructionProgressItemId",
                principalTable: "ConstructionProgressItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionReports_ConstructionProgressItems_ConstructionPro~",
                table: "InspectionReports");

            migrationBuilder.RenameColumn(
                name: "ConstructionProgressItemId",
                table: "InspectionReports",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_InspectionReports_ConstructionProgressItemId",
                table: "InspectionReports",
                newName: "IX_InspectionReports_ProjectId");

            migrationBuilder.AddColumn<int>(
                name: "PlanId",
                table: "InspectionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressId",
                table: "InspectionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReports_PlanId",
                table: "InspectionReports",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionReports_ProgressId",
                table: "InspectionReports",
                column: "ProgressId");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReports_ConstructionPlans_PlanId",
                table: "InspectionReports",
                column: "PlanId",
                principalTable: "ConstructionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReports_ConstructionProgresses_ProgressId",
                table: "InspectionReports",
                column: "ProgressId",
                principalTable: "ConstructionProgresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionReports_Projects_ProjectId",
                table: "InspectionReports",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
