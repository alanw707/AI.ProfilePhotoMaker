using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class CleanupProcessedImageDeletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear all ProcessedImages data as requested
            migrationBuilder.Sql("DELETE FROM ProcessedImages;");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "IsMarkedForDeletion",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "UserRequestedDeletionDate",
                table: "ProcessedImages");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 13, 4, 23, 50, 614, DateTimeKind.Utc).AddTicks(8525));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 13, 4, 23, 50, 614, DateTimeKind.Utc).AddTicks(8533));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 13, 4, 23, 50, 614, DateTimeKind.Utc).AddTicks(8536));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 13, 4, 23, 50, 614, DateTimeKind.Utc).AddTicks(8539));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProcessedImages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProcessedImages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMarkedForDeletion",
                table: "ProcessedImages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UserRequestedDeletionDate",
                table: "ProcessedImages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 7, 20, 59, 27, 39, DateTimeKind.Utc).AddTicks(3420));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 7, 20, 59, 27, 39, DateTimeKind.Utc).AddTicks(3423));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 7, 20, 59, 27, 39, DateTimeKind.Utc).AddTicks(3429));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 7, 20, 59, 27, 39, DateTimeKind.Utc).AddTicks(3431));
        }
    }
}
