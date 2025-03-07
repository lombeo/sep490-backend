using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class addsiteSurvey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSurveys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSurveys");
        }
    }
}
