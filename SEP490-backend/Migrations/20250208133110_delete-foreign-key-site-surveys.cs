using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deleteforeignkeysitesurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurveys_Projects_ProjectId",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_ProjectId",
                table: "SiteSurveys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_ProjectId",
                table: "SiteSurveys",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurveys_Projects_ProjectId",
                table: "SiteSurveys",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
