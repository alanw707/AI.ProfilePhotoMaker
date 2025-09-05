using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RestoreDigitalNativeStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 4, 20, 1, 28, 0, DateTimeKind.Utc);

            // Restore Digital Native style to ID 20 (overwriting Academic)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 5, 1, 52, 57, 540, DateTimeKind.Utc);

            // Revert back to Academic style
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
        }
    }
}