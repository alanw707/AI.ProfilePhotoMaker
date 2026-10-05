using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerRoadmapTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerRoadmapReplans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreservedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppliedRoadmapId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerRoadmapReplans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerRoadmapReplans_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerRoadmapTaskProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MilestoneDay = table.Column<int>(type: "int", nullable: true),
                    PlannedEffortHours = table.Column<double>(type: "float", nullable: true),
                    DependsOnJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EffortHours = table.Column<double>(type: "float", nullable: true),
                    OutputNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LinkedMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerRoadmapTaskProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerRoadmapTaskProgress_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmapReplans_OwnerId_CreatedAt",
                table: "CareerRoadmapReplans",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmapReplans_RoadmapId",
                table: "CareerRoadmapReplans",
                column: "RoadmapId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmapTaskProgress_OwnerId",
                table: "CareerRoadmapTaskProgress",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmapTaskProgress_RoadmapId_TaskId",
                table: "CareerRoadmapTaskProgress",
                columns: new[] { "RoadmapId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerRoadmapReplans");

            migrationBuilder.DropTable(
                name: "CareerRoadmapTaskProgress");
        }
    }
}
