using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerMarketBriefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerMarketBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: false),
                    OccupationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccupationTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OewsRelease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectionsRelease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PublishedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextActionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerMarketBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerMarketBriefs_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerMarketBriefs_OwnerId_CreatedAt",
                table: "CareerMarketBriefs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerMarketBriefs_RunId",
                table: "CareerMarketBriefs",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerMarketBriefs");
        }
    }
}
