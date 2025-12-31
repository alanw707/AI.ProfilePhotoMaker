using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCreditPurchaseTransactionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(449));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(454));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(458));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(702), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(703) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(811), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(812) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(815), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(816) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(819), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(819) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(822), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(822) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(825), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(825) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(828), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(828) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(832), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(833) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(835), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(836) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(838), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(838) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(842), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(843) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(847), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(848) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(853), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(854) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(857), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(858) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(862), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(863) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(866), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(866) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(871), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(871) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(874), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(874) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(877), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(877) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(881), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(881) });

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Styles WHERE Id = 21 OR Name = 'digital-native')
                BEGIN
                    INSERT INTO Styles (Id, CreatedAt, Description, IsActive, Name, NegativePromptTemplate, PromptTemplate, UpdatedAt)
                    VALUES (
                        21,
                        '2025-09-26T12:03:55.4210884Z',
                        'Modern tech creator portrait',
                        1,
                        'digital-native',
                        'outdated technology, old fashioned, formal business, analog aesthetic, traditional office',
                        '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality',
                        '2025-09-26T12:03:55.4210884Z'
                    )
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_PaymentTransactionId_Unique",
                table: "CreditPurchases",
                column: "PaymentTransactionId",
                unique: true,
                filter: "[PaymentTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditPurchases_PaymentTransactionId_Unique",
                table: "CreditPurchases");

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2260));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2262));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2264));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2418), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2419) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2422), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2423) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2425), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2425) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2427), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2427) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2429), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2429) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2431), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2432) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2433), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2434) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2435), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2436) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2437), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2438) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2439), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2440) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2441), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2442) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2443), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2444) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2445), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2445) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2446), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2447) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2448), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2449) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2450), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2450) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2452), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2452) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2454), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2454) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2456), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2460) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2462), new DateTime(2025, 9, 5, 3, 20, 24, 677, DateTimeKind.Utc).AddTicks(2462) });
        }
    }
}
