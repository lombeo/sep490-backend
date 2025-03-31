using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sep490_Backend.Migrations
{
    /// <inheritdoc />
    public partial class deletesomeunneed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailMaterials");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailUsers");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemDetailVehicles");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemQAs");

            migrationBuilder.DropColumn(
                name: "PlanQuantity",
                table: "ConstructPlanItems");

            migrationBuilder.DropColumn(
                name: "PlanTotalPrice",
                table: "ConstructPlanItems");

            migrationBuilder.DropColumn(
                name: "QA",
                table: "ConstructPlanItems");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "ConstructPlanItemDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "ConstructPlanItemDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ConstructPlanItemDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleId",
                table: "ConstructPlanItemDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ConstructPlanItems_Index",
                table: "ConstructPlanItems",
                column: "Index");

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
                name: "IX_ConstructPlanItemDetails_MaterialId",
                table: "ConstructPlanItemDetails",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_UserId",
                table: "ConstructPlanItemDetails",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetails_VehicleId",
                table: "ConstructPlanItemDetails",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemQAMembers_ConstructPlanItemWorkCode",
                table: "ConstructPlanItemQAMembers",
                column: "ConstructPlanItemWorkCode");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_ConstructionTeams_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "ConstructionTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_MaterialId",
                table: "ConstructPlanItemDetails",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_UserId",
                table: "ConstructPlanItemDetails",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_ResourceId",
                table: "ConstructPlanItemDetails",
                column: "ResourceId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_VehicleId",
                table: "ConstructPlanItemDetails",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems",
                column: "ParentIndex",
                principalTable: "ConstructPlanItems",
                principalColumn: "Index",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_ConstructionTeams_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_MaterialId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Materials_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Users_UserId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItemDetails_Vehicles_VehicleId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems");

            migrationBuilder.DropTable(
                name: "ConstructPlanItemQAMembers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ConstructPlanItems_Index",
                table: "ConstructPlanItems");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItemDetails_MaterialId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItemDetails_ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItemDetails_UserId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_ConstructPlanItemDetails_VehicleId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "ConstructPlanItemDetails");

            migrationBuilder.AddColumn<decimal>(
                name: "PlanQuantity",
                table: "ConstructPlanItems",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlanTotalPrice",
                table: "ConstructPlanItems",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<List<int>>(
                name: "QA",
                table: "ConstructPlanItems",
                type: "integer[]",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_ConstructPlanItemDetailMaterials_ConstructPlanItemDetailId",
                table: "ConstructPlanItemDetailMaterials",
                column: "ConstructPlanItemDetailId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_ConstructPlanItems_ConstructPlanItems_ParentIndex",
                table: "ConstructPlanItems",
                column: "ParentIndex",
                principalTable: "ConstructPlanItems",
                principalColumn: "WorkCode",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
