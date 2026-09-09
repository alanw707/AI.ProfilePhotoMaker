using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeWebhookOperationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentTransactionId",
                table: "CouponRedemptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StripeWebhookOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationKey = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    StripeEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeWebhookOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_PaymentTransactionId",
                table: "CouponRedemptions",
                column: "PaymentTransactionId",
                unique: true,
                filter: "[PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StripeWebhookOperations_OperationKey",
                table: "StripeWebhookOperations",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StripeWebhookOperations_Status_LeaseExpiresAt",
                table: "StripeWebhookOperations",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StripeWebhookOperations_StripeEventId",
                table: "StripeWebhookOperations",
                column: "StripeEventId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CouponRedemptions_PaymentTransactions_PaymentTransactionId",
                table: "CouponRedemptions",
                column: "PaymentTransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CouponRedemptions_PaymentTransactions_PaymentTransactionId",
                table: "CouponRedemptions");

            migrationBuilder.DropTable(
                name: "StripeWebhookOperations");

            migrationBuilder.DropIndex(
                name: "IX_CouponRedemptions_PaymentTransactionId",
                table: "CouponRedemptions");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionId",
                table: "CouponRedemptions");
        }
    }
}
