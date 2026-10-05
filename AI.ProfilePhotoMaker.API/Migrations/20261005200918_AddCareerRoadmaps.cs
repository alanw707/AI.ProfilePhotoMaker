using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerRoadmaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerRoadmaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SelectedOption = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: false),
                    OccupationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccupationTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MarketBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeeklyEffortHours = table.Column<double>(type: "float", nullable: false),
                    LowTimeNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OmittedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerRoadmaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerRoadmaps_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmaps_OwnerId_Version",
                table: "CareerRoadmaps",
                columns: new[] { "OwnerId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerRoadmaps_RunId",
                table: "CareerRoadmaps",
                column: "RunId",
                unique: true,
                filter: "[RunId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerRoadmaps");
        }
    }
}
