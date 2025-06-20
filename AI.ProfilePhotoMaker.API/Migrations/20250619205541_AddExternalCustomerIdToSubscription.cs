using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCustomerIdToSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerId",
                table: "Subscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(111), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(112) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(114), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(115) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(117), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(117) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(119), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(119) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(121), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(121) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(124), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(124) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(126), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(126) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(128), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(128) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(130), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(130) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(132), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(132) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "basic-plan",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(296), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(297) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(301), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(302) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(356), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(357) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(359), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(359) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(362), new DateTime(2025, 6, 19, 20, 55, 40, 857, DateTimeKind.Utc).AddTicks(362) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalCustomerId",
                table: "Subscriptions");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2072), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2073) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2075), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2076) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2078), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2078) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2080), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2080) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2083), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2083) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2085), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2086) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2087), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2088) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2089), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2089) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2092), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2092) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2094), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2094) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "basic-plan",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2240), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2241) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2245), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2245) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2248), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2248) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2251), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2251) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2253), new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2254) });
        }
    }
}
