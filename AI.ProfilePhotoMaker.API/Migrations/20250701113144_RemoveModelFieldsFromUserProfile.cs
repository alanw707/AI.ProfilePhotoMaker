using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveModelFieldsFromUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelTrainedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TrainedModelId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TrainedModelVersionId",
                table: "UserProfiles");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 11, 31, 43, 801, DateTimeKind.Utc).AddTicks(7945));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 11, 31, 43, 801, DateTimeKind.Utc).AddTicks(7948));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 11, 31, 43, 801, DateTimeKind.Utc).AddTicks(7952));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 11, 31, 43, 801, DateTimeKind.Utc).AddTicks(7954));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModelTrainedAt",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainedModelId",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainedModelVersionId",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 4, 34, 54, 315, DateTimeKind.Utc).AddTicks(7525));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 4, 34, 54, 315, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 4, 34, 54, 315, DateTimeKind.Utc).AddTicks(7530));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 1, 4, 34, 54, 315, DateTimeKind.Utc).AddTicks(7532));
        }
    }
}
