using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestDateToResourceReqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RequestDate",
                table: "ResourceAllocationReqs",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestDate",
                table: "ResourceAllocationReqs");
        }
    }
}
