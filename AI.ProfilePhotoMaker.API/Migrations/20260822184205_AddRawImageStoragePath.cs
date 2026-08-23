using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRawImageStoragePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawImageStoragePath",
                table: "ProcessedImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE ProcessedImages
                SET RawImageStoragePath = SUBSTRING(FailureReason, LEN('raw-preview:') + 1, 4000),
                    FailureReason = NULL
                WHERE FailureReason LIKE 'raw-preview:%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE ProcessedImages
                SET FailureReason = 'raw-preview:' + RawImageStoragePath
                WHERE RawImageStoragePath IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "RawImageStoragePath",
                table: "ProcessedImages");
        }
    }
}
