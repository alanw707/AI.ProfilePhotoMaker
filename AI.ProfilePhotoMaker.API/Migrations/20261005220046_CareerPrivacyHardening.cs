using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class CareerPrivacyHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReplayFailedAt",
                table: "CareerTombstones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplayedAt",
                table: "CareerTombstones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CareerAllowances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing allowances were only dated by their month; PeriodStart is the best (and a safe, early) stamp.
            migrationBuilder.Sql("UPDATE [CareerAllowances] SET [CreatedAt] = [PeriodStart]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplayFailedAt",
                table: "CareerTombstones");

            migrationBuilder.DropColumn(
                name: "ReplayedAt",
                table: "CareerTombstones");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CareerAllowances");
        }
    }
}
