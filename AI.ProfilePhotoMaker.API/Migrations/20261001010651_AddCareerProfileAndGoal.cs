using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerProfileAndGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActiveVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerGoals_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActiveVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerProfiles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerGoalVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareerGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    TargetRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TargetLocation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    WorkArrangement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DesiredPayMin = table.Column<int>(type: "int", nullable: true),
                    DesiredPayMax = table.Column<int>(type: "int", nullable: true),
                    WeeklyEffortHours = table.Column<int>(type: "int", nullable: true),
                    BasedOnProfileVersion = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerGoalVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerGoalVersions_CareerGoals_CareerGoalId",
                        column: x => x.CareerGoalId,
                        principalTable: "CareerGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    YearsExperience = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Highlights = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkArrangement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RestoredFromVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerProfileVersions_CareerProfiles_CareerProfileId",
                        column: x => x.CareerProfileId,
                        principalTable: "CareerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerGoals_OwnerId",
                table: "CareerGoals",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerGoalVersions_CareerGoalId_VersionNumber",
                table: "CareerGoalVersions",
                columns: new[] { "CareerGoalId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerGoalVersions_OwnerId",
                table: "CareerGoalVersions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfiles_OwnerId",
                table: "CareerProfiles",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileVersions_CareerProfileId_VersionNumber",
                table: "CareerProfileVersions",
                columns: new[] { "CareerProfileId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileVersions_OwnerId",
                table: "CareerProfileVersions",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerGoalVersions");

            migrationBuilder.DropTable(
                name: "CareerProfileVersions");

            migrationBuilder.DropTable(
                name: "CareerGoals");

            migrationBuilder.DropTable(
                name: "CareerProfiles");
        }
    }
}
