using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSpiritualWithDigitalNative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Skip this migration since ID 20 is not spiritual in production
            // The AddDigitalNativeStyle migration will add the digital-native style as a new entry
            migrationBuilder.Sql("SELECT 1"); // No-op SQL to make migration valid
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op rollback since the Up method was a no-op
            migrationBuilder.Sql("SELECT 1"); // No-op SQL to make migration valid
        }
    }
}
