using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixUserTeamRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams");

            migrationBuilder.DropColumn(
                name: "ManagedTeamId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamId1",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams");

            migrationBuilder.AddColumn<int>(
                name: "ManagedTeamId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId1",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId1",
                table: "Users",
                column: "TeamId1");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId1",
                table: "Users",
                column: "TeamId1",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
