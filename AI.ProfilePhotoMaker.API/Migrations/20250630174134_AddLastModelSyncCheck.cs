using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLastModelSyncCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastModelSyncCheck",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 17, 41, 33, 611, DateTimeKind.Utc).AddTicks(3299));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 17, 41, 33, 611, DateTimeKind.Utc).AddTicks(3302));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 17, 41, 33, 611, DateTimeKind.Utc).AddTicks(3304));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 17, 41, 33, 611, DateTimeKind.Utc).AddTicks(3306));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastModelSyncCheck",
                table: "UserProfiles");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9960));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9963));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9964));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9966));
        }
    }
}
