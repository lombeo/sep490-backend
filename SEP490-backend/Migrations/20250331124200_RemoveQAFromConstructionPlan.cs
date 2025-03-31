using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQAFromConstructionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructPlanItemQAMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructPlanItemQAMembers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ConstructPlanItemWorkCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemQAMembers", x => new { x.UserId, x.ConstructPlanItemWorkCode });
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemQAMembers_ConstructPlanItems_ConstructPlanItemWorkCode",
                        column: x => x.ConstructPlanItemWorkCode,
                        principalTable: "ConstructPlanItems",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemQAMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemQAMembers_ConstructPlanItemWorkCode",
                table: "ConstructPlanItemQAMembers",
                column: "ConstructPlanItemWorkCode");
        }
    }
}
