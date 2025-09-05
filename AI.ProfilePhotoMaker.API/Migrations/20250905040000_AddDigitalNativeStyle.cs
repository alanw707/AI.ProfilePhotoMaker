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
            // Add digital-native style as a new entry using direct SQL to avoid seeding issues
            migrationBuilder.Sql(@"
                INSERT INTO Styles (Name, Description, PromptTemplate, NegativePromptTemplate, IsActive, CreatedAt, UpdatedAt)
                VALUES (
                    'digital-native', 
                    'Modern tech creator portrait',
                    '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality',
                    'outdated technology, old fashioned, formal business, analog aesthetic, traditional office',
                    1,
                    GETUTCDATE(),
                    GETUTCDATE()
                )
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove digital-native style using direct SQL
            migrationBuilder.Sql("DELETE FROM Styles WHERE Name = 'digital-native'");
        }
    }
}