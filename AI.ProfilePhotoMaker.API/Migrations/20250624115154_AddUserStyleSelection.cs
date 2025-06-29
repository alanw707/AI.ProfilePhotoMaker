using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStyleSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserStyleSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StyleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStyleSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStyleSelections_Styles_StyleId",
                        column: x => x.StyleId,
                        principalTable: "Styles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserStyleSelections_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 11, 51, 54, 66, DateTimeKind.Utc).AddTicks(3810));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Description" },
                values: new object[] { new DateTime(2025, 6, 24, 11, 51, 54, 66, DateTimeKind.Utc).AddTicks(3813), "Most popular - great for professionals" });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 11, 51, 54, 66, DateTimeKind.Utc).AddTicks(3816));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Description" },
                values: new object[] { new DateTime(2025, 6, 24, 11, 51, 54, 66, DateTimeKind.Utc).AddTicks(3818), "Maximum credits for agencies and enterprises" });

            migrationBuilder.CreateIndex(
                name: "IX_UserStyleSelections_StyleId",
                table: "UserStyleSelections",
                column: "StyleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStyleSelections_UserProfileId_StyleId",
                table: "UserStyleSelections",
                columns: new[] { "UserProfileId", "StyleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStyleSelections");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 23, 4, 12, 5, 950, DateTimeKind.Utc).AddTicks(4832));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Description" },
                values: new object[] { new DateTime(2025, 6, 23, 4, 12, 5, 950, DateTimeKind.Utc).AddTicks(4835), "Most popular - great for professionals needing multiple styles" });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 23, 4, 12, 5, 950, DateTimeKind.Utc).AddTicks(4837));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Description" },
                values: new object[] { new DateTime(2025, 6, 23, 4, 12, 5, 950, DateTimeKind.Utc).AddTicks(4839), "Maximum credits for agencies and heavy users" });
        }
    }
}
