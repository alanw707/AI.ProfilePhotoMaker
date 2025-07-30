using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudioPackAndUpdatePackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 29, 4, 49, 27, 984, DateTimeKind.Utc).AddTicks(6138));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 29, 4, 49, 27, 984, DateTimeKind.Utc).AddTicks(6141));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DisplayOrder" },
                values: new object[] { new DateTime(2025, 7, 29, 4, 49, 27, 984, DateTimeKind.Utc).AddTicks(6143), 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 14, 12, 41, 23, 771, DateTimeKind.Utc).AddTicks(2240));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 14, 12, 41, 23, 771, DateTimeKind.Utc).AddTicks(2243));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DisplayOrder" },
                values: new object[] { new DateTime(2025, 7, 14, 12, 41, 23, 771, DateTimeKind.Utc).AddTicks(2248), 4 });

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BonusCredits", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[] { 3, 100, new DateTime(2025, 7, 14, 12, 41, 23, 771, DateTimeKind.Utc).AddTicks(2245), 300, "Best value for content creators and businesses", 3, true, "Studio Pack", 39.99m, null, null, null });
        }
    }
}
