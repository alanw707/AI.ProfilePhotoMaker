using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingEmailLogPostmarkMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostmarkMessageId",
                table: "MarketingEmailLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingEmailLogs_PostmarkMessageId",
                table: "MarketingEmailLogs",
                column: "PostmarkMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketingEmailLogs_PostmarkMessageId",
                table: "MarketingEmailLogs");

            migrationBuilder.DropColumn(
                name: "PostmarkMessageId",
                table: "MarketingEmailLogs");
        }
    }
}
