using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deleteforeignkeyContractPlanItemDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_ConstructionTeams_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "ResourceMobilizationReqs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_RequestType",
                table: "ResourceMobilizationReqs",
                column: "RequestType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceMobilizationReqs_RequestType",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "ResourceMobilizationReqs");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_ConstructionTeams_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
