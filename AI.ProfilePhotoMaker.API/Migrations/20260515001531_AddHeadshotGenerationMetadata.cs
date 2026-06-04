using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadshotGenerationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ProcessedImages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditCost",
                table: "ProcessedImages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ProcessedImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationMode",
                table: "ProcessedImages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationStatus",
                table: "ProcessedImages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "ProcessedImages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "ProcessedImages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderModel",
                table: "ProcessedImages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_User_Mode_CreatedAt",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "GenerationMode", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_User_Mode_CreatedAt",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "CreditCost",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "GenerationMode",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "GenerationStatus",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ProcessedImages");

            migrationBuilder.DropColumn(
                name: "ProviderModel",
                table: "ProcessedImages");
        }
    }
}
