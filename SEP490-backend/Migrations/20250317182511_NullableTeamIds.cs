using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class NullableTeamIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_ConstructionTeamId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ConstructionTeamId",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ConstructionTeamId",
                table: "Users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId1",
                table: "Users",
                column: "TeamId1",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamId1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamId1",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ConstructionTeamId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ConstructionTeamId",
                table: "Users",
                column: "ConstructionTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_ConstructionTeamId",
                table: "Users",
                column: "ConstructionTeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
