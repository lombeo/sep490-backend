using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class DropConstructionLogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogResources");

            migrationBuilder.DropTable(
                name: "LogWorkAmounts");

            migrationBuilder.DropTable(
                name: "ConstructionLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Advice = table.Column<string>(type: "text", nullable: true),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Images = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    LogCode = table.Column<string>(type: "text", nullable: false),
                    LogDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LogName = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Problem = table.Column<string>(type: "text", nullable: true),
                    Progress = table.Column<int>(type: "integer", nullable: true),
                    Quality = table.Column<int>(type: "integer", nullable: true),
                    Safety = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Updater = table.Column<int>(type: "integer", nullable: false),
                    Weather = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<string>(type: "text", nullable: true),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TaskIndex = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogResources_ConstructionLogs_LogId",
                        column: x => x.LogId,
                        principalTable: "ConstructionLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogWorkAmounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TaskIndex = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Updater = table.Column<int>(type: "integer", nullable: false),
                    WorkAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogWorkAmounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogWorkAmounts_ConstructionLogs_LogId",
                        column: x => x.LogId,
                        principalTable: "ConstructionLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionLogs_ProjectId",
                table: "ConstructionLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LogResources_LogId",
                table: "LogResources",
                column: "LogId");

            migrationBuilder.CreateIndex(
                name: "IX_LogWorkAmounts_LogId",
                table: "LogWorkAmounts",
                column: "LogId");
        }
    }
}
