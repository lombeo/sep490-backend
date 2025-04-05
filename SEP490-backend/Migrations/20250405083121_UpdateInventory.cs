using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "ResourceInventory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "ResourceInventory",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "ResourceInventory");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "ResourceInventory");
        }
    }
}
