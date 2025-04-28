using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVehicleImageAttachmentToJsonDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create temporary columns to hold JSON data
            migrationBuilder.AddColumn<JsonDocument>(
                name: "Image_Json",
                table: "Vehicles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Attachment_Json",
                table: "Vehicles",
                type: "jsonb",
                nullable: true);

            // Convert Image strings to JSON
            migrationBuilder.Sql(@"
                UPDATE ""Vehicles"" 
                SET ""Image_Json"" = jsonb_build_array(
                    jsonb_build_object(
                        'Id', md5(random()::text),
                        'Name', 'Migrated Image',
                        'WebViewLink', ""Image"",
                        'WebContentLink', ""Image""
                    )
                ) 
                WHERE ""Image"" IS NOT NULL AND ""Image"" != '';

                UPDATE ""Vehicles"" 
                SET ""Image_Json"" = '[]'::jsonb 
                WHERE ""Image"" IS NULL OR ""Image"" = '';
            ");

            // Convert Attachment strings to JSON
            migrationBuilder.Sql(@"
                UPDATE ""Vehicles"" 
                SET ""Attachment_Json"" = jsonb_build_array(
                    jsonb_build_object(
                        'Id', md5(random()::text),
                        'Name', 'Migrated Attachment',
                        'WebViewLink', ""Attachment"",
                        'WebContentLink', ""Attachment""
                    )
                ) 
                WHERE ""Attachment"" IS NOT NULL AND ""Attachment"" != '';

                UPDATE ""Vehicles"" 
                SET ""Attachment_Json"" = '[]'::jsonb 
                WHERE ""Attachment"" IS NULL OR ""Attachment"" = '';
            ");

            // Drop old columns
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Attachment",
                table: "Vehicles");

            // Rename new columns to the original names
            migrationBuilder.RenameColumn(
                name: "Image_Json",
                table: "Vehicles",
                newName: "Image");

            migrationBuilder.RenameColumn(
                name: "Attachment_Json",
                table: "Vehicles",
                newName: "Attachment");

            // Ensure columns are not nullable
            migrationBuilder.AlterColumn<JsonDocument>(
                name: "Image",
                table: "Vehicles",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("[]"),
                oldClrType: typeof(JsonDocument),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<JsonDocument>(
                name: "Attachment",
                table: "Vehicles",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("[]"),
                oldClrType: typeof(JsonDocument),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Create temporary string columns
            migrationBuilder.AddColumn<string>(
                name: "Image_String",
                table: "Vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attachment_String",
                table: "Vehicles",
                type: "text",
                nullable: true);

            // Convert JSON to strings (taking the first item's WebContentLink if it exists)
            migrationBuilder.Sql(@"
                UPDATE ""Vehicles"" 
                SET ""Image_String"" = COALESCE((""Image""->0->>'WebContentLink'), '')
                WHERE ""Image"" IS NOT NULL;

                UPDATE ""Vehicles"" 
                SET ""Attachment_String"" = COALESCE((""Attachment""->0->>'WebContentLink'), '')
                WHERE ""Attachment"" IS NOT NULL;
            ");

            // Drop JSON columns
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Attachment",
                table: "Vehicles");

            // Rename string columns to original names
            migrationBuilder.RenameColumn(
                name: "Image_String",
                table: "Vehicles",
                newName: "Image");

            migrationBuilder.RenameColumn(
                name: "Attachment_String",
                table: "Vehicles",
                newName: "Attachment");

            // Ensure columns are not nullable
            migrationBuilder.AlterColumn<string>(
                name: "Image",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Attachment",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
