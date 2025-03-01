using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachment",
                table: "Projects");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Attachments",
                table: "Projects",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachments",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "Attachment",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TaxCode",
                table: "Customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
