using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class PlanEditLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanEditLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LockAcquiredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LockExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanEditLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanEditLocks_ConstructionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ConstructionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanEditLocks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanEditLocks_LockExpiresAt",
                table: "PlanEditLocks",
                column: "LockExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlanEditLocks_PlanId",
                table: "PlanEditLocks",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanEditLocks_UserId",
                table: "PlanEditLocks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanEditLocks");
        }
    }
}
