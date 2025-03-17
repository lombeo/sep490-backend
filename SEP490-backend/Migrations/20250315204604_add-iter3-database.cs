using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sep490_Backend.DTO.ResourceReqs;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class additer3database : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Driver",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts");

            migrationBuilder.AddColumn<int>(
                name: "ConstructionTeamId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "SiteSurveys",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "SiteSurveys",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Index",
                table: "ContractDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reviewer = table.Column<Dictionary<int, bool>>(type: "jsonb", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeamManager = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionTeams_Users_TeamManager",
                        column: x => x.TeamManager,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MadeIn = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ChassisNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    WholesalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Inventory = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    Attachment = table.Column<string>(type: "text", nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProductionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAllocationReqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromProjectId = table.Column<int>(type: "integer", nullable: false),
                    ToProjectId = table.Column<int>(type: "integer", nullable: false),
                    RequestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResourceAllocationDetails = table.Column<List<RequestDetails>>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PriorityLevel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserId1 = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAllocationReqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_FromProject",
                        column: x => x.FromProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_ToProject",
                        column: x => x.ToProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_Users_Creator",
                        column: x => x.Creator,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_Users_Updater",
                        column: x => x.Updater,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourceAllocationReqs_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceMobilizationReqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    RequestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResourceMobilizationDetails = table.Column<List<RequestDetails>>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PriorityLevel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserId1 = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMobilizationReqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceMobilizationReqs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceMobilizationReqs_Users_Creator",
                        column: x => x.Creator,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceMobilizationReqs_Users_Updater",
                        column: x => x.Updater,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceMobilizationReqs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourceMobilizationReqs_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ConstructionPlanReviewers",
                columns: table => new
                {
                    ReviewedPlanId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionPlanReviewers", x => new { x.ReviewedPlanId, x.ReviewerId });
                    table.ForeignKey(
                        name: "FK_ConstructionPlanReviewers_ConstructionPlans_ReviewedPlanId",
                        column: x => x.ReviewedPlanId,
                        principalTable: "ConstructionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructionPlanReviewers_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItems",
                columns: table => new
                {
                    WorkCode = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    ParentIndex = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WorkName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlanQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlanTotalPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    QA = table.Column<List<int>>(type: "integer[]", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItems", x => x.WorkCode);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                        column: x => x.ParentIndex,
                        principalTable: "ConstructPlanItems",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItems_ConstructionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ConstructionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionTeamPlanItems",
                columns: table => new
                {
                    ConstructionTeamId = table.Column<int>(type: "integer", nullable: false),
                    ConstructPlanItemWorkCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionTeamPlanItems", x => new { x.ConstructionTeamId, x.ConstructPlanItemWorkCode });
                    table.ForeignKey(
                        name: "FK_ConstructionTeamPlanItems_ConstructPlanItems_ConstructPlanItemWorkCode",
                        column: x => x.ConstructPlanItemWorkCode,
                        principalTable: "ConstructPlanItems",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructionTeamPlanItems_ConstructionTeams_ConstructionTeamId",
                        column: x => x.ConstructionTeamId,
                        principalTable: "ConstructionTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItemDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetails_ConstructPlanItems_PlanItemId",
                        column: x => x.PlanItemId,
                        principalTable: "ConstructPlanItems",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItemQAs",
                columns: table => new
                {
                    QAItemWorkCode = table.Column<string>(type: "text", nullable: false),
                    QAMemberId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemQAs", x => new { x.QAItemWorkCode, x.QAMemberId });
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemQAs_ConstructPlanItems_QAItemWorkCode",
                        column: x => x.QAItemWorkCode,
                        principalTable: "ConstructPlanItems",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemQAs_Users_QAMemberId",
                        column: x => x.QAMemberId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItemDetailMaterials",
                columns: table => new
                {
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    ConstructPlanItemDetailId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemDetailMaterials", x => new { x.MaterialId, x.ConstructPlanItemDetailId });
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailMaterials_ConstructPlanItemDetails_ConstructPlanItemDetailId",
                        column: x => x.ConstructPlanItemDetailId,
                        principalTable: "ConstructPlanItemDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItemDetailUsers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ResourceAllocationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemDetailUsers", x => new { x.UserId, x.ResourceAllocationId });
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailUsers_ConstructPlanItemDetails_ResourceAllocationId",
                        column: x => x.ResourceAllocationId,
                        principalTable: "ConstructPlanItemDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructPlanItemDetailVehicles",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    ConstructPlanItemDetailId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructPlanItemDetailVehicles", x => new { x.VehicleId, x.ConstructPlanItemDetailId });
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailVehicles_ConstructPlanItemDetails_ConstructPlanItemDetailId",
                        column: x => x.ConstructPlanItemDetailId,
                        principalTable: "ConstructPlanItemDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstructPlanItemDetailVehicles_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Driver",
                table: "Vehicles",
                column: "Driver",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ConstructionTeamId",
                table: "Users",
                column: "ConstructionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId",
                table: "SiteSurveys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId1",
                table: "SiteSurveys",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDetails_ParentIndex",
                table: "ContractDetails",
                column: "ParentIndex");

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogs_CreatedAt",
                table: "ActionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogs_LogType",
                table: "ActionLogs",
                column: "LogType");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionPlanReviewers_ReviewerId",
                table: "ConstructionPlanReviewers",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionPlans_PlanName",
                table: "ConstructionPlans",
                column: "PlanName");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionPlans_ProjectId",
                table: "ConstructionPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeamPlanItems_ConstructPlanItemWorkCode",
                table: "ConstructionTeamPlanItems",
                column: "ConstructPlanItemWorkCode");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionTeams_TeamName",
                table: "ConstructionTeams",
                column: "TeamName");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetailMaterials_ConstructPlanItemDetailId",
                table: "ConstructPlanItemDetailMaterials",
                column: "ConstructPlanItemDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_PlanItemId",
                table: "ConstructPlanItemDetails",
                column: "PlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_ResourceType",
                table: "ConstructPlanItemDetails",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_WorkCode",
                table: "ConstructPlanItemDetails",
                column: "WorkCode");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetailUsers_ResourceAllocationId",
                table: "ConstructPlanItemDetailUsers",
                column: "ResourceAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetailVehicles_ConstructPlanItemDetailId",
                table: "ConstructPlanItemDetailVehicles",
                column: "ConstructPlanItemDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemQAs_QAMemberId",
                table: "ConstructPlanItemQAs",
                column: "QAMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_EndDate",
                table: "ConstructPlanItems",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems",
                column: "ParentIndex");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_PlanId",
                table: "ConstructPlanItems",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItems_StartDate",
                table: "ConstructPlanItems",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_Creator",
                table: "ResourceAllocationReqs",
                column: "Creator");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_FromProjectId",
                table: "ResourceAllocationReqs",
                column: "FromProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_RequestCode",
                table: "ResourceAllocationReqs",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_Status",
                table: "ResourceAllocationReqs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_ToProjectId",
                table: "ResourceAllocationReqs",
                column: "ToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_Updater",
                table: "ResourceAllocationReqs",
                column: "Updater");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_UserId",
                table: "ResourceAllocationReqs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocationReqs_UserId1",
                table: "ResourceAllocationReqs",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_Creator",
                table: "ResourceMobilizationReqs",
                column: "Creator");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_ProjectId",
                table: "ResourceMobilizationReqs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_RequestCode",
                table: "ResourceMobilizationReqs",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_RequestDate",
                table: "ResourceMobilizationReqs",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_Status",
                table: "ResourceMobilizationReqs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_Updater",
                table: "ResourceMobilizationReqs",
                column: "Updater");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_UserId",
                table: "ResourceMobilizationReqs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMobilizationReqs_UserId1",
                table: "ResourceMobilizationReqs",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractDetails_ContractDetails_ParentIndex",
                table: "ContractDetails",
                column: "ParentIndex",
                principalTable: "ContractDetails",
                principalColumn: "WorkCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurveys_Users_UserId",
                table: "SiteSurveys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteSurveys_Users_UserId1",
                table: "SiteSurveys",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_ConstructionTeamId",
                table: "Users",
                column: "ConstructionTeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractDetails_ContractDetails_ParentIndex",
                table: "ContractDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurveys_Users_UserId",
                table: "SiteSurveys");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteSurveys_Users_UserId1",
                table: "SiteSurveys");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_ConstructionTeamId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ConstructionTeams_TeamId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ActionLogs");

            migrationBuilder.DropTable(
                name: "ConstructionPlanReviewers");

            migrationBuilder.DropTable(
                name: "ConstructionTeamPlanItems");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailMaterials");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailUsers");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailVehicles");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemQAs");

            migrationBuilder.DropTable(
                name: "ResourceAllocationReqs");

            migrationBuilder.DropTable(
                name: "ResourceMobilizationReqs");

            migrationBuilder.DropTable(
                name: "ConstructionTeams");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetails");

            migrationBuilder.DropTable(
                name: "ConstructPlanItems");

            migrationBuilder.DropTable(
                name: "ConstructionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Driver",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Users_ConstructionTeamId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_UserId",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_SiteSurveys_UserId1",
                table: "SiteSurveys");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_ContractDetails_ParentIndex",
                table: "ContractDetails");

            migrationBuilder.DropColumn(
                name: "ConstructionTeamId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SiteSurveys");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "SiteSurveys");

            migrationBuilder.AlterColumn<string>(
                name: "Index",
                table: "ContractDetails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Driver",
                table: "Vehicles",
                column: "Driver");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts",
                column: "ProjectId",
                unique: true);
        }
    }
}
