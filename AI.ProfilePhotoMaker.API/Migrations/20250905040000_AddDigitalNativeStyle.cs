using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalNativeStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 5, 4, 0, 0, 0, DateTimeKind.Utc);

            // Add digital-native style as a new entry (don't try to update ID 20 which is academic)
            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[] { 
                    "digital-native", 
                    "Modern tech creator portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality",
                    "outdated technology, old fashioned, formal business, analog aesthetic, traditional office",
                    true,
                    updateTime,
                    updateTime
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove digital-native style
            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Name",
                keyValue: "digital-native");
        }
    }
}