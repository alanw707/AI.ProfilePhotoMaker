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
            var updateTime = new DateTime(2025, 9, 5, 3, 20, 25, 0, DateTimeKind.Utc);

            // Step 1: Replace spiritual style (ID 20) with digital-native data
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "IsActive", "UpdatedAt" },
                values: new object[] { 
                    "digital-native", 
                    "Modern tech creator portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality",
                    "outdated technology, old fashioned, formal business, analog aesthetic, traditional office",
                    true, // Ensure it's active
                    updateTime
                });

            // Step 2: Deactivate any other spiritual styles (safety measure)
            migrationBuilder.Sql(@"
                UPDATE Styles 
                SET IsActive = 0
                WHERE Name LIKE '%spiritual%' AND Id != 20
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var rollbackTime = new DateTime(2025, 9, 5, 1, 52, 57, 540, DateTimeKind.Utc);

            // Step 1: Restore spiritual style back to ID 20
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "IsActive", "UpdatedAt" },
                values: new object[] { 
                    "spiritual", 
                    "Spiritual wellness style",
                    "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic",
                    "materialistic look, stressed appearance, conventional business",
                    true, // Keep it active for rollback
                    rollbackTime
                });

            // Step 2: Reactivate any spiritual styles that were deactivated (safety measure)
            migrationBuilder.Sql(@"
                UPDATE Styles 
                SET IsActive = 1
                WHERE Name LIKE '%spiritual%'
            ");
        }
    }
}
