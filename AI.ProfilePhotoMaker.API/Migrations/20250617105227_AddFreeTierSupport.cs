using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeTierSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles");

            migrationBuilder.AddColumn<int>(
                name: "FreeCredits",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCreditReset",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 17, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionTier",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "UsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreditsCost = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditsRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsageLogs_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9326), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9326) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9328), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9329) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9330), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9331) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9332), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9333) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9335), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9335) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9337), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9337) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9339), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9339) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9341), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9341) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9343), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9343) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9345), new DateTime(2025, 6, 17, 10, 52, 27, 197, DateTimeKind.Utc).AddTicks(9345) });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLogs_UserId",
                table: "UsageLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageLogs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_UserProfiles_UserId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "FreeCredits",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastCreditReset",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "SubscriptionTier",
                table: "UserProfiles");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2199), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2199) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2201), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2202) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2204), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2204) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2206), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2206) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2208), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2208) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2210), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2210) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2212), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2212) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2214), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2214) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2216), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2216) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2217), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2218) });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }
    }
}
