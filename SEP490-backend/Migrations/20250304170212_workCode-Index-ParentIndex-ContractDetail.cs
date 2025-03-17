using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class workCodeIndexParentIndexContractDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ContractDetails",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ContractDetails");

            migrationBuilder.AddColumn<string>(
                name: "WorkCode",
                table: "ContractDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Index",
                table: "ContractDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ParentIndex",
                table: "ContractDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContractDetails",
                table: "ContractDetails",
                column: "WorkCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ContractDetails",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "WorkCode",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "Index",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "ParentIndex",
                table: "ContractDetails");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ContractDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "ContractDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContractDetails",
                table: "ContractDetails",
                column: "Id");
        }
    }
}
