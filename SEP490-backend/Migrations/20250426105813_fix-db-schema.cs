using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class fixdbschema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Users_Creator",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Users_Updater",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Users_UserId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Users_UserId1",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_Creator",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_Updater",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_UserId",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_UserId1",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurveys_Users_UserId",
                table: "SiteSurveys");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurveys_Users_UserId1",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_UserId",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_UserId1",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_ResourceMobilizationReqs_UserId",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceMobilizationReqs_UserId1",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_UserId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_UserId1",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SiteSurveys");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "SiteSurveys");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ResourceAllocationReqs");

            migrationBuilder.AddColumn<int>(
                name: "UsedQuantity",
                table: "ConstructionProgressItemDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_Creator",
                table: "SiteSurveys",
                column: "Creator");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_Updater",
                table: "SiteSurveys",
                column: "Updater");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Approver",
                table: "ResourceAllocationReqs",
                column: "Updater",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Requester",
                table: "ResourceAllocationReqs",
                column: "Creator",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Approver",
                table: "ResourceMobilizationReqs",
                column: "Updater",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Requester",
                table: "ResourceMobilizationReqs",
                column: "Creator",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurvey_Approver",
                table: "SiteSurveys",
                column: "Updater",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurvey_Conductor",
                table: "SiteSurveys",
                column: "Creator",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Approver",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceAllocationReqs_Requester",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Approver",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMobilizationReqs_Requester",
                table: "ResourceMobilizationReqs");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurvey_Approver",
                table: "SiteSurveys");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurvey_Conductor",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_Creator",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_Updater",
                table: "SiteSurveys");

            migrationBuilder.DropColumn(
                name: "UsedQuantity",
                table: "ConstructionProgressItemDetails");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "SiteSurveys",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "SiteSurveys",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ResourceMobilizationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "ResourceMobilizationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ResourceAllocationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "ResourceAllocationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId",
                table: "SiteSurveys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId1",
                table: "SiteSurveys",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_UserId",
                table: "ResourceMobilizationReqs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_UserId1",
                table: "ResourceMobilizationReqs",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_UserId",
                table: "ResourceAllocationReqs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_UserId1",
                table: "ResourceAllocationReqs",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Users_Creator",
                table: "ResourceAllocationReqs",
                column: "Creator",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Users_Updater",
                table: "ResourceAllocationReqs",
                column: "Updater",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Users_UserId",
                table: "ResourceAllocationReqs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceAllocationReqs_Users_UserId1",
                table: "ResourceAllocationReqs",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_Creator",
                table: "ResourceMobilizationReqs",
                column: "Creator",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_Updater",
                table: "ResourceMobilizationReqs",
                column: "Updater",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_UserId",
                table: "ResourceMobilizationReqs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMobilizationReqs_Users_UserId1",
                table: "ResourceMobilizationReqs",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurveys_Users_UserId",
                table: "SiteSurveys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurveys_Users_UserId1",
                table: "SiteSurveys",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
