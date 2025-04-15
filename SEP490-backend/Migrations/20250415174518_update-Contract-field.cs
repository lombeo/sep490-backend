using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class updateContractfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyRepName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CompanyRepTitle",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CompletionDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CustomerRepName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CustomerRepTitle",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SignedDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Contracts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyRepName",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRepTitle",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletionDate",
                table: "Contracts",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerRepName",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerRepTitle",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedDate",
                table: "Contracts",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "Contracts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
