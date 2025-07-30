using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEnterpriseAddStudioPack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 29, 11, 15, 1, 759, DateTimeKind.Utc).AddTicks(5635));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 29, 11, 15, 1, 759, DateTimeKind.Utc).AddTicks(5638));

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BonusCredits", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[] { 3, 100, new DateTime(2025, 7, 29, 11, 15, 1, 759, DateTimeKind.Utc).AddTicks(5640), 300, "Best value for content creators and businesses", 3, true, "Studio Pack", 39.99m, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BonusCredits", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[] { 4, 250, new DateTime(2025, 7, 29, 4, 49, 27, 984, DateTimeKind.Utc).AddTicks(6143), 750, "Maximum credits for agencies and enterprises", 3, true, "Enterprise Pack", 79.99m, null, null, null });
        }
    }
}
