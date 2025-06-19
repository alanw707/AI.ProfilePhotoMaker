using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameFreeCreditsToCreds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FreeCredits",
                table: "UserProfiles",
                newName: "Credits");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5184), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5185) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5187), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5187) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5189), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5189) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5191), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5191) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5193), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5193) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5195), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5195) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5197), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5197) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5199), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5199) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5201), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5201) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5203), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5203) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Credits",
                table: "UserProfiles",
                newName: "FreeCredits");

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
        }
    }
}
