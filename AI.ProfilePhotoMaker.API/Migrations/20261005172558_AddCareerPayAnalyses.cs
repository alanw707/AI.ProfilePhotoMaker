using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerPayAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerPayAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: false),
                    OccupationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccupationTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AreaTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaResolution = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocationInput = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RequestedAnnual = table.Column<int>(type: "int", nullable: true),
                    OewsRelease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OewsSnapshotSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProjectionsRelease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RuleVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ObservationSourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualificationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerPayAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerPayAnalyses_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerPayAnalyses_OwnerId_CreatedAt",
                table: "CareerPayAnalyses",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerPayAnalyses_RunId",
                table: "CareerPayAnalyses",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerPayAnalyses");
        }
    }
}
