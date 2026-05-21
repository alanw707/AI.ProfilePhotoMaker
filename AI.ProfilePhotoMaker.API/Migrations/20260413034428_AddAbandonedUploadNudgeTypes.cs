using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedUploadNudgeTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AbandonedUploadNudgeLogs_UserId",
                table: "AbandonedUploadNudgeLogs");

            migrationBuilder.AddColumn<string>(
                name: "NudgeType",
                table: "AbandonedUploadNudgeLogs",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "no-uploads");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedUploadNudgeLogs_UserId_NudgeType",
                table: "AbandonedUploadNudgeLogs",
                columns: new[] { "UserId", "NudgeType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AbandonedUploadNudgeLogs_UserId_NudgeType",
                table: "AbandonedUploadNudgeLogs");

            migrationBuilder.DropColumn(
                name: "NudgeType",
                table: "AbandonedUploadNudgeLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedUploadNudgeLogs_UserId",
                table: "AbandonedUploadNudgeLogs",
                column: "UserId",
                unique: true);
        }
    }
}
