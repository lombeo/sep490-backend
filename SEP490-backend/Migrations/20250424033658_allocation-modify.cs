using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class allocationmodify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromTaskId",
                table: "ResourceAllocationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "ResourceAllocationReqs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ToTaskId",
                table: "ResourceAllocationReqs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_FromTaskId",
                table: "ResourceAllocationReqs",
                column: "FromTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_RequestType",
                table: "ResourceAllocationReqs",
                column: "RequestType");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_ToTaskId",
                table: "ResourceAllocationReqs",
                column: "ToTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_FromTaskId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_RequestType",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropIndex(
                name: "IX_ResourceAllocationReqs_ToTaskId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropColumn(
                name: "FromTaskId",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "ResourceAllocationReqs");

            migrationBuilder.DropColumn(
                name: "ToTaskId",
                table: "ResourceAllocationReqs");
        }
    }
}
