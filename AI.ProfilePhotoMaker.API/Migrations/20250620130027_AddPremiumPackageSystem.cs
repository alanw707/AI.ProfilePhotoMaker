using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumPackageSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PremiumPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    MaxStyles = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxImagesPerStyle = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPackagePurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreditsRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainedModelId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ModelTrainedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPackagePurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPackagePurchases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPackagePurchases_PremiumPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PremiumPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PremiumPackages",
                columns: new[] { "Id", "CreatedAt", "Credits", "Description", "IsActive", "MaxImagesPerStyle", "MaxStyles", "Name", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8974), 5, "Generate up to 4 professional photos with 2 different styles", true, 2, 2, "Quick Shot", 9.99m, null },
                    { 2, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8976), 15, "Generate up to 14 professional photos with 5 different styles", true, 3, 5, "Professional", 19.99m, null },
                    { 3, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8978), 35, "Generate up to 34 professional photos with 8 different styles", true, 4, 8, "Premium Studio", 34.99m, null },
                    { 4, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8980), 50, "Generate up to 49 professional photos with 10 different styles", true, 5, 10, "Ultimate", 49.99m, null }
                });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8697), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8697) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8700), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8701) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8703), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8703) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8705), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8705) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8707), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8707) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8709), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8709) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8711), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8711) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8714), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8714) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8716), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8716) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8718), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8718) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "basic-plan",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8933), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8933) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8938), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8939) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8941), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8941) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-monthly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8944), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8944) });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-yearly",
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8946), new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8947) });

            migrationBuilder.CreateIndex(
                name: "IX_PremiumPackages_Name",
                table: "PremiumPackages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPackagePurchases_PackageId",
                table: "UserPackagePurchases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPackagePurchases_UserId",
                table: "UserPackagePurchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPackagePurchases");

            migrationBuilder.DropTable(
                name: "PremiumPackages");

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
    }
}
