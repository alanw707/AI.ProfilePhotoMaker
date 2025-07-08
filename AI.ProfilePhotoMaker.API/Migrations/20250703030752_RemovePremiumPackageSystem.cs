using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RemovePremiumPackageSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPackagePurchases");

            migrationBuilder.DropTable(
                name: "PremiumPackages");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 3, 7, 52, 248, DateTimeKind.Utc).AddTicks(6495));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 3, 7, 52, 248, DateTimeKind.Utc).AddTicks(6498));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 3, 7, 52, 248, DateTimeKind.Utc).AddTicks(6500));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 3, 7, 52, 248, DateTimeKind.Utc).AddTicks(6502));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PremiumPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxImagesPerStyle = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStyles = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
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
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreditsRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModelTrainedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PaymentTransactionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TrainedModelId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 2, 45, 11, 557, DateTimeKind.Utc).AddTicks(1977));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 2, 45, 11, 557, DateTimeKind.Utc).AddTicks(1981));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 2, 45, 11, 557, DateTimeKind.Utc).AddTicks(1983));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 3, 2, 45, 11, 557, DateTimeKind.Utc).AddTicks(1985));

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
    }
}
