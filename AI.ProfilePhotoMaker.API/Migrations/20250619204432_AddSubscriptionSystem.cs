using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BillingPeriod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImagesPerMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    CanTrainCustomModels = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanBatchGenerate = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighResolutionOutput = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxTrainingImages = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStylesAccess = table.Column<int>(type: "INTEGER", nullable: false),
                    StripeProductId = table.Column<string>(type: "TEXT", nullable: true),
                    StripePriceId = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PlanId = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalSubscriptionId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelAtPeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

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

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "BillingPeriod", "CanBatchGenerate", "CanTrainCustomModels", "CreatedAt", "Description", "HighResolutionOutput", "ImagesPerMonth", "IsActive", "MaxStylesAccess", "MaxTrainingImages", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[,]
                {
                    { "basic-plan", "monthly", false, false, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2240), "Perfect for casual users who want to enhance their photos", false, 3, true, 1, 0, "Basic", 0.00m, null, null, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2241) },
                    { "premium-monthly", "monthly", true, true, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2245), "Ideal for professionals who need custom AI models and advanced features", true, 50, true, 5, 20, "Premium", 19.99m, null, null, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2245) },
                    { "premium-yearly", "yearly", true, true, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2248), "Premium features with 2 months free when billed annually", true, 50, true, 5, 20, "Premium (Yearly)", 199.99m, null, null, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2248) },
                    { "pro-monthly", "monthly", true, true, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2251), "For businesses and power users who need unlimited access", true, 200, true, 10, 50, "Pro", 49.99m, null, null, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2251) },
                    { "pro-yearly", "yearly", true, true, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2253), "Pro features with 2 months free when billed annually", true, 200, true, 10, 50, "Pro (Yearly)", 499.99m, null, null, new DateTime(2025, 6, 19, 20, 44, 31, 729, DateTimeKind.Utc).AddTicks(2254) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SubscriptionId",
                table: "PaymentTransactions",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_UserId",
                table: "PaymentTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5184), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5185) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5187), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5187) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5189), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5189) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5191), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5191) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5193), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5193) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5195), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5195) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5197), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5197) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5199), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5199) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5201), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5201) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5203), new DateTime(2025, 6, 18, 21, 57, 28, 253, DateTimeKind.Utc).AddTicks(5203) });
        }
    }
}
