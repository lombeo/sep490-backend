using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVehicleImageAndAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachment",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "Vehicles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "Attachment",
                table: "Vehicles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Image",
                table: "Vehicles",
                type: "jsonb",
                nullable: false);
        }
    }
}
