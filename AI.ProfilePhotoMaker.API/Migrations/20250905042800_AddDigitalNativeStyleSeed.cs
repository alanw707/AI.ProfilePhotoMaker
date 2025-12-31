using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalNativeStyleSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Styles WHERE Id = 21 OR Name = 'digital-native')
                BEGIN
                    INSERT INTO Styles (Id, CreatedAt, Description, IsActive, Name, NegativePromptTemplate, PromptTemplate, UpdatedAt)
                    VALUES (
                        21,
                        '2025-09-05T04:28:00.0000000Z',
                        'Modern tech creator portrait',
                        1,
                        'digital-native',
                        'outdated technology, old fashioned, formal business, analog aesthetic, traditional office',
                        '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality',
                        '2025-09-05T04:28:00.0000000Z'
                    )
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21);
        }
    }
}
