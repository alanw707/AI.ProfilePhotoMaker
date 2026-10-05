using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerOccupationMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OccupationCode",
                table: "CareerGoalVersions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OccupationMatchId",
                table: "CareerGoalVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccupationReferenceRelease",
                table: "CareerGoalVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccupationTitle",
                table: "CareerGoalVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CareerOccupationMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: true),
                    ReferenceRelease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MatcherVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClarificationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ConfirmedIntoGoalVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerOccupationMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerOccupationMatches_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerOccupationMatches_OwnerId_CreatedAt",
                table: "CareerOccupationMatches",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerOccupationMatches_RunId",
                table: "CareerOccupationMatches",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerOccupationMatches");

            migrationBuilder.DropColumn(
                name: "OccupationCode",
                table: "CareerGoalVersions");

            migrationBuilder.DropColumn(
                name: "OccupationMatchId",
                table: "CareerGoalVersions");

            migrationBuilder.DropColumn(
                name: "OccupationReferenceRelease",
                table: "CareerGoalVersions");

            migrationBuilder.DropColumn(
                name: "OccupationTitle",
                table: "CareerGoalVersions");
        }
    }
}
