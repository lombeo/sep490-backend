using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deletedescriptionresourceInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "ResourceInventory");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceInventory_ResourceId_ProjectId_ResourceType",
                table: "ResourceInventory",
                columns: new[] { "ResourceId", "ProjectId", "ResourceType" },
                filter: "\"Deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceInventory_ResourceId_ProjectId_ResourceType",
                table: "ResourceInventory");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ResourceInventory",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
