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
    public partial class fixdatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Fax = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DirectorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    BankAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
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
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "text", nullable: false),
                    ProjectName = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    ConstructType = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Area = table.Column<string>(type: "text", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    TechnicalReqs = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractCode = table.Column<string>(type: "text", nullable: false),
                    ContractName = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EstimatedDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SignDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ContractDetails",
                columns: table => new
                {
                    WorkCode = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    ParentIndex = table.Column<string>(type: "text", maxLength: 50, nullable: true),
                    WorkName = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDetails", x => x.WorkCode);
                    table.ForeignKey(
                        name: "FK_ContractDetails_ContractDetails_ParentIndex",
                        column: x => x.ParentIndex,
                        principalTable: "ContractDetails",
                        principalColumn: "WorkCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractDetails_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
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
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<bool>(type: "boolean", nullable: false),
                    Dob = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsVerify = table.Column<bool>(type: "boolean", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    ConstructionTeamId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_ConstructionTeams_ConstructionTeamId",
                        column: x => x.ConstructionTeamId,
                        principalTable: "ConstructionTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_ConstructionTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "ConstructionTeams",
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
                name: "ProjectUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IsCreator = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUsers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expiry_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "SiteSurveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    SiteSurveyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConstructionRequirements = table.Column<string>(type: "text", nullable: true),
                    EquipmentRequirements = table.Column<string>(type: "text", nullable: true),
                    HumanResourceCapacity = table.Column<string>(type: "text", nullable: true),
                    RiskAssessment = table.Column<string>(type: "text", nullable: true),
                    BiddingDecision = table.Column<int>(type: "integer", nullable: false),
                    ProfitAssessment = table.Column<string>(type: "text", nullable: true),
                    BidWinProb = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedProfits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenderPackagePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBidPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountRate = table.Column<double>(type: "double precision", nullable: false),
                    ProjectCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FinalProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    Attachments = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    SurveyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_SiteSurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteSurveys_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiteSurveys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SiteSurveys_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LicensePlate = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    YearOfManufacture = table.Column<int>(type: "integer", nullable: false),
                    CountryOfManufacture = table.Column<string>(type: "text", nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    ChassisNumber = table.Column<string>(type: "text", nullable: false),
                    EngineNumber = table.Column<string>(type: "text", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Driver = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    FuelType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FuelTankVolume = table.Column<int>(type: "integer", nullable: false),
                    FuelUnit = table.Column<string>(type: "text", nullable: false),
                    Attachment = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Users_Driver",
                        column: x => x.Driver,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_ContractDetails_ContractId",
                table: "ContractDetails",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDetails_ParentIndex",
                table: "ContractDetails",
                column: "ParentIndex");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Deleted",
                table: "EmailTemplates",
                column: "Deleted");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerId",
                table: "Projects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUsers_ProjectId_UserId",
                table: "ProjectUsers",
                columns: new[] { "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUsers_UserId",
                table: "ProjectUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "RefreshTokens",
                column: "user_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_ProjectId",
                table: "SiteSurveys",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId",
                table: "SiteSurveys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSurveys_UserId1",
                table: "SiteSurveys",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ConstructionTeamId",
                table: "Users",
                column: "ConstructionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Deleted",
                table: "Users",
                column: "Deleted");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FullName",
                table: "Users",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Gender",
                table: "Users",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsVerify",
                table: "Users",
                column: "IsVerify");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Driver",
                table: "Vehicles",
                column: "Driver",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructionPlanReviewers_Users_ReviewerId",
                table: "ConstructionPlanReviewers",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructionTeamPlanItems_ConstructionTeams_ConstructionTeamId",
                table: "ConstructionTeamPlanItems",
                column: "ConstructionTeamId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructionTeams_Users_TeamManager",
                table: "ConstructionTeams",
                column: "TeamManager",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructionTeams_Users_TeamManager",
                table: "ConstructionTeams");

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
                name: "ContractDetails");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "ProjectUsers");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ResourceAllocationReqs");

            migrationBuilder.DropTable(
                name: "ResourceMobilizationReqs");

            migrationBuilder.DropTable(
                name: "SiteSurveys");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetails");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "ConstructPlanItems");

            migrationBuilder.DropTable(
                name: "ConstructionPlans");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ConstructionTeams");
        }
    }
}
