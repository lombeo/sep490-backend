using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deletesitesurvey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSurveys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSurveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Attachments = table.Column<string>(type: "text", nullable: true),
                    BidWinProb = table.Column<double>(type: "double precision", nullable: false),
                    BiddingDecision = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    ConstructionRequirements = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Creator = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountRate = table.Column<double>(type: "double precision", nullable: false),
                    EquipmentRequirements = table.Column<string>(type: "text", nullable: true),
                    EstimatedExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedProfits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FinalProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HumanResourceCapacity = table.Column<string>(type: "text", nullable: true),
                    ProfitAssessment = table.Column<string>(type: "text", nullable: true),
                    ProjectCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    RiskAssessment = table.Column<string>(type: "text", nullable: true),
                    SiteSurveyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SurveyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenderPackagePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBidPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Updater = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSurveys", x => x.Id);
                });
        }
    }
}
