using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class addConstructionProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionProgresses_ConstructionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ConstructionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructionProgresses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionProgressItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgressId = table.Column<int>(type: "integer", nullable: false),
                    WorkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentIndex = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WorkName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PlanStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PlanEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ItemRelations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionProgressItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionProgressItems_ConstructionProgresses_ProgressId",
                        column: x => x.ProgressId,
                        principalTable: "ConstructionProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionProgressItemDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgressItemId = table.Column<int>(type: "integer", nullable: false),
                    WorkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ResourceId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionProgressItemDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionProgressItemDetails_ConstructionProgressItems_P~",
                        column: x => x.ProgressItemId,
                        principalTable: "ConstructionProgressItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgresses_PlanId",
                table: "ConstructionProgresses",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgresses_ProjectId",
                table: "ConstructionProgresses",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItemDetails_ProgressItemId",
                table: "ConstructionProgressItemDetails",
                column: "ProgressItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItemDetails_ResourceId",
                table: "ConstructionProgressItemDetails",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItemDetails_ResourceType",
                table: "ConstructionProgressItemDetails",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItems_ProgressId",
                table: "ConstructionProgressItems",
                column: "ProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItems_ProgressId_Index",
                table: "ConstructionProgressItems",
                columns: new[] { "ProgressId", "Index" },
                unique: true,
                filter: "\"Deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItems_Status",
                table: "ConstructionProgressItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionProgressItems_WorkCode",
                table: "ConstructionProgressItems",
                column: "WorkCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionProgressItemDetails");

            migrationBuilder.DropTable(
                name: "ConstructionProgressItems");

            migrationBuilder.DropTable(
                name: "ConstructionProgresses");
        }
    }
}
