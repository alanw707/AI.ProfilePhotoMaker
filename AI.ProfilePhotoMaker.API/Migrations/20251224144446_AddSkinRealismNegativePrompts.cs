using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSkinRealismNegativePrompts : Migration
    {
        private const string SkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DECLARE @skin nvarchar(1000) = '{SkinRealismNegativePrompt}';

UPDATE dbo.Styles
SET NegativePromptTemplate =
        CASE
            WHEN NULLIF(LTRIM(RTRIM(NegativePromptTemplate)), '') IS NULL THEN @skin
            ELSE CONCAT(NegativePromptTemplate, ', ', @skin)
        END,
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1
  AND (NegativePromptTemplate IS NULL OR NegativePromptTemplate NOT LIKE '%waxy skin%');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DECLARE @skin nvarchar(1000) = '{SkinRealismNegativePrompt}';

UPDATE dbo.Styles
SET NegativePromptTemplate =
        CASE
            WHEN NegativePromptTemplate = @skin THEN ''
            ELSE LTRIM(RTRIM(REPLACE(NegativePromptTemplate, ', ' + @skin, '')))
        END,
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1
  AND NegativePromptTemplate LIKE '%waxy skin%';
");
        }
    }
}
