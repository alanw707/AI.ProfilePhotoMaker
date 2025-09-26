using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixAcademicSpiritualCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 5, 1, 52, 57, 540, DateTimeKind.Utc);

            // Step 1: Restore Academic to ID 20 (production had Academic at ID 20, not Spiritual)
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] {
                    "academic",
                    "Academic professional style",
                    "academic professional portrait, scholarly appearance, intellectual style, educational setting",
                    "unprofessional, casual, distracting elements",
                    updateTime
                });

            // Step 2: Remove Spiritual style (deactivate it)
            migrationBuilder.Sql(@"
                UPDATE Styles 
                SET IsActive = 0
                WHERE Name LIKE '%spiritual%'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 4, 22, 17, 33, 383, DateTimeKind.Utc);

            // Step 1: Revert ID 20 back to Digital Native (as it was after our original migration)
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] {
                    "digital-native",
                    "Modern tech creator portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality",
                    "outdated technology, old fashioned, formal business, analog aesthetic, traditional office",
                    updateTime
                });

            // Step 2: Reactivate Spiritual style
            migrationBuilder.Sql(@"
                UPDATE Styles 
                SET IsActive = 1
                WHERE Name LIKE '%spiritual%'
            ");
        }
    }
}
