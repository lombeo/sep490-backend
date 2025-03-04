using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachment",
                table: "Contracts");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Attachments",
                table: "Contracts",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachments",
                table: "Contracts");

            migrationBuilder.AddColumn<string>(
                name: "Attachment",
                table: "Contracts",
                type: "text",
                nullable: true);
        }
    }
}
