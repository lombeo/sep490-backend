using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class ConstructionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    LogCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LogName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LogDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Resources = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    WorkAmount = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Weather = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Safety = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Quality = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Progress = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Problem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Advice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Images = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionLogs_LogCode",
                table: "ConstructionLogs",
                column: "LogCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionLogs_LogDate",
                table: "ConstructionLogs",
                column: "LogDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionLogs_ProjectId",
                table: "ConstructionLogs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionLogs");
        }
    }
}
