using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MaterialId",
                table: "CareerAgentRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CareerMaterialProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerMaterialProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerMaterialProposals_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: false),
                    OccupationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerMaterials_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerMaterialVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: false),
                    OccupationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RestoredFromVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerMaterialVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerMaterialVersions_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterialProposals_OwnerId_MaterialId",
                table: "CareerMaterialProposals",
                columns: new[] { "OwnerId", "MaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterialProposals_RunId",
                table: "CareerMaterialProposals",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterials_OwnerId_Kind_UpdatedAt",
                table: "CareerMaterials",
                columns: new[] { "OwnerId", "Kind", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterials_RunId",
                table: "CareerMaterials",
                column: "RunId",
                unique: true,
                filter: "[RunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterialVersions_MaterialId_Number",
                table: "CareerMaterialVersions",
                columns: new[] { "MaterialId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerMaterialVersions_OwnerId",
                table: "CareerMaterialVersions",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerMaterialProposals");

            migrationBuilder.DropTable(
                name: "CareerMaterials");

            migrationBuilder.DropTable(
                name: "CareerMaterialVersions");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "CareerAgentRuns");
        }
    }
}
