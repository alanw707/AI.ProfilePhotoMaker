using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RetentionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add retention policy columns
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
                name: "ScheduledDeletionDate",
                table: "ProcessedImages",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UserRequestedDeletionDate",
                table: "ProcessedImages",
                type: "TEXT",
                nullable: true);

            // Update existing records with proper scheduled deletion dates
            // For original uploads (IsOriginalUpload = 1): CreatedAt + 7 days
            // For generated images (IsGenerated = 1): CreatedAt + 30 days  
            // For other images: CreatedAt + 30 days (default)
            migrationBuilder.Sql(@"
                UPDATE ProcessedImages 
                SET ScheduledDeletionDate = datetime(CreatedAt, '+7 days')
                WHERE IsOriginalUpload = 1;
            ");

            migrationBuilder.Sql(@"
                UPDATE ProcessedImages 
                SET ScheduledDeletionDate = datetime(CreatedAt, '+30 days')
                WHERE IsGenerated = 1 OR (IsOriginalUpload = 0 AND IsGenerated = 0);
            ");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7296));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7301));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "ScheduledDeletionDate",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "UserRequestedDeletionDate",
                table: "ProcessedImages");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 22, 14, 42, 921, DateTimeKind.Utc).AddTicks(7964));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 22, 14, 42, 921, DateTimeKind.Utc).AddTicks(7966));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 22, 14, 42, 921, DateTimeKind.Utc).AddTicks(7968));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 24, 22, 14, 42, 921, DateTimeKind.Utc).AddTicks(7970));
        }
    }
}
