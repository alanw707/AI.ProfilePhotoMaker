using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixProductionStyleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Match database styles exactly to the 21 image files in blob storage
            
            // Remove "professional" style (no matching image file)
            migrationBuilder.Sql("DELETE FROM Styles WHERE Name = 'professional'");
            
            // Add missing styles that have image files
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Styles WHERE Name = 'author')
                INSERT INTO Styles (Name, Description, PromptTemplate, NegativePromptTemplate, IsActive, CreatedAt, UpdatedAt)
                VALUES ('author', 'Author and writer portrait', 
                       'author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression',
                       'unprofessional, distracting elements, poor composition', 1, GETUTCDATE(), GETUTCDATE())
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback changes
            migrationBuilder.Sql("DELETE FROM Styles WHERE Name = 'author'");
            
            // Restore professional style
            migrationBuilder.Sql(@"
                INSERT INTO Styles (Name, Description, PromptTemplate, NegativePromptTemplate, IsActive, CreatedAt, UpdatedAt)
                VALUES ('professional', 'Classic professional headshot for general business use', 
                       'professional headshot, business attire, clean background, confident expression, high-quality photography',
                       'casual clothes, blurred, low quality, unprofessional', 1, GETUTCDATE(), GETUTCDATE())
            ");
        }
    }
}