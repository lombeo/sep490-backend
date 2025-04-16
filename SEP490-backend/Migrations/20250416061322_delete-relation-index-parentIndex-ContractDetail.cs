using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deleterelationindexparentIndexContractDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractDetails_ContractDetails_ParentIndex",
                table: "ContractDetails");

            migrationBuilder.DropIndex(
                name: "IX_ContractDetails_ParentIndex",
                table: "ContractDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ParentIndex",
                table: "ContractDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ParentIndex",
                table: "ContractDetails",
                type: "text",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractDetails_ParentIndex",
                table: "ContractDetails",
                column: "ParentIndex");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractDetails_ContractDetails_ParentIndex",
                table: "ContractDetails",
                column: "ParentIndex",
                principalTable: "ContractDetails",
                principalColumn: "WorkCode",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
