using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCreativeArtisticPromptsV2 : Migration
    {
        private const string SkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string DefaultQualityNegativePrompt =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands";

        private const string OriginalCreativePromptTemplate =
            "{subject}, creative professional portrait of {gender} {ethnicity}, stylish artistic attire, expressive personality, creative studio background";

        private const string OriginalCreativeNegativePromptTemplate =
            "corporate formal, traditional business, boring conventional look, " + SkinRealismNegativePrompt;

        private const string OriginalArtisticPromptTemplate =
            "{subject}, artistic portrait of {gender} {ethnicity}, creative artistic style, expressive artistic look, bohemian creative appearance";

        private const string OriginalArtisticNegativePromptTemplate =
            "corporate business, formal attire, conventional look, " + SkinRealismNegativePrompt;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DECLARE @skin nvarchar(1000) = '" + SkinRealismNegativePrompt + "';\n" +
                "DECLARE @quality nvarchar(1000) = '" + DefaultQualityNegativePrompt + "';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, creative professional portrait of {gender} {ethnicity}, modern creative director vibe, stylish contemporary outfit, subtle colorful studio or gallery background, bright airy daylight, playful confident expression, clean editorial photography, head-and-shoulders framing, natural skin texture, minimal retouching, sharp focus, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'creative';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, fine-art portrait photography of {gender} {ethnicity}, cinematic moody studio lighting, chiaroscuro, textured backdrop, cinematic color grading, contemplative expression, artistic composition, subtle film grain, head-and-shoulders framing, natural skin texture, minimal retouching, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'artistic';\n");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalCreativePromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalCreativeNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'creative';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalArtisticPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalArtisticNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'artistic';\n");
        }
    }
}
