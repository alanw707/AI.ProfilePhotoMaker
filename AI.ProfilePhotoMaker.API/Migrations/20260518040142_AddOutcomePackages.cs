using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOutcomePackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutcomePackageDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StripePriceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InternalCreditPackageId = table.Column<int>(type: "int", nullable: true),
                    IncludedCandidateCount = table.Column<int>(type: "int", nullable: false),
                    IncludedRefinementCount = table.Column<int>(type: "int", nullable: false),
                    IncludedPremiumAugmentationCount = table.Column<int>(type: "int", nullable: false),
                    IncludesPlatformExportKit = table.Column<bool>(type: "bit", nullable: false),
                    IncludesScoreDelta = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutcomePackageDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutcomePackageDefinitions_CreditPackages_InternalCreditPackageId",
                        column: x => x.InternalCreditPackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserPackageEntitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OutcomePackageDefinitionId = table.Column<int>(type: "int", nullable: false),
                    SourcePaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RemainingPackageUses = table.Column<int>(type: "int", nullable: false),
                    RemainingCandidates = table.Column<int>(type: "int", nullable: false),
                    RemainingRefinements = table.Column<int>(type: "int", nullable: false),
                    RemainingPremiumAugmentations = table.Column<int>(type: "int", nullable: false),
                    PlatformExportKitAvailable = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPackageEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPackageEntitlements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPackageEntitlements_OutcomePackageDefinitions_OutcomePackageDefinitionId",
                        column: x => x.OutcomePackageDefinitionId,
                        principalTable: "OutcomePackageDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPackageEntitlements_PaymentTransactions_SourcePaymentTransactionId",
                        column: x => x.SourcePaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "OutcomePackageDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "Currency", "Description", "DisplayOrder", "IncludedCandidateCount", "IncludedPremiumAugmentationCount", "IncludedRefinementCount", "IncludesPlatformExportKit", "IncludesScoreDelta", "InternalCreditPackageId", "IsActive", "Name", "Price", "StripePriceId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "free_preview", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "Score your source photo and try a friendly low-resolution preview before buying a package.", 1, 1, 0, 0, false, false, null, true, "Free Preview", 0m, null, null },
                    { 2, "starter_package", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "Three profile-photo candidates, best shot selector, basic adjustment, and selected platform exports.", 2, 3, 0, 2, true, false, 1, true, "Starter Package", 9.99m, null, null },
                    { 3, "pro_package", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "Nine candidates, best shot selector, score delta, exports, refinements, and premium augmentations.", 3, 9, 3, 5, true, true, 2, true, "Pro Package", 19.99m, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutcomePackageDefinitions_Code_Unique",
                table: "OutcomePackageDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutcomePackageDefinitions_InternalCreditPackageId",
                table: "OutcomePackageDefinitions",
                column: "InternalCreditPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_OutcomePackageDefinitions_IsActive_DisplayOrder",
                table: "OutcomePackageDefinitions",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPackageEntitlements_OutcomePackageDefinitionId",
                table: "UserPackageEntitlements",
                column: "OutcomePackageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPackageEntitlements_SourcePaymentTransactionId_Unique",
                table: "UserPackageEntitlements",
                column: "SourcePaymentTransactionId",
                unique: true,
                filter: "[SourcePaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserPackageEntitlements_User_Status_CreatedAt",
                table: "UserPackageEntitlements",
                columns: new[] { "UserId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPackageEntitlements");

            migrationBuilder.DropTable(
                name: "OutcomePackageDefinitions");
        }
    }
}
