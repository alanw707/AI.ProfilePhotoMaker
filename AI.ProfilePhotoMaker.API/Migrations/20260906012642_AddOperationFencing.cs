using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationFencing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationToken",
                table: "StripeWebhookOperations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GenerationOperationToken",
                table: "ProcessedImages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationToken",
                table: "HeadshotGenerationOperations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperationToken",
                table: "StripeWebhookOperations");

            migrationBuilder.DropColumn(
                name: "GenerationOperationToken",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "OperationToken",
                table: "HeadshotGenerationOperations");
        }
    }
}
