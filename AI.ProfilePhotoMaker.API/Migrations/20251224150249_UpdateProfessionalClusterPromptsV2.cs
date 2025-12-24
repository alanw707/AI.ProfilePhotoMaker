using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProfessionalClusterPromptsV2 : Migration
    {
        private const string SkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string DefaultQualityNegativePrompt =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands";

        private const string OriginalLinkedInPromptTemplate =
            "{subject}, LinkedIn profile photo of {gender} {ethnicity}, professional networking style, business-casual attire, warm smile, neutral background, natural lighting";

        private const string OriginalLinkedInNegativePromptTemplate =
            "casual clothes, distracting background, unprofessional, " + SkinRealismNegativePrompt;

        private const string OriginalTechProfessionalPromptTemplate =
            "{subject}, tech professional portrait of {gender} {ethnicity}, modern technology industry style, smart casual tech attire, innovative professional look";

        private const string OriginalTechProfessionalNegativePromptTemplate =
            "overly formal, outdated style, unprofessional, " + SkinRealismNegativePrompt;

        private const string OriginalStartupPromptTemplate =
            "{subject}, startup professional portrait of {gender} {ethnicity}, innovative tech style, modern casual business attire, entrepreneurial spirit";

        private const string OriginalStartupNegativePromptTemplate =
            "overly formal, traditional corporate, stiff pose, " + SkinRealismNegativePrompt;

        private const string OriginalEntrepreneurPromptTemplate =
            "{subject}, entrepreneur portrait of {gender} {ethnicity}, innovative business leader style, modern professional attire, dynamic confident expression";

        private const string OriginalEntrepreneurNegativePromptTemplate =
            "formal corporate look, traditional, static pose, " + SkinRealismNegativePrompt;

        private const string OriginalCasualPromptTemplate =
            "{subject}, casual lifestyle portrait of {gender} {ethnicity}, comfortable everyday attire, natural relaxed expression, home or outdoor setting";

        private const string OriginalCasualNegativePromptTemplate =
            "overly formal, stiff corporate, too dressy, " + SkinRealismNegativePrompt;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DECLARE @skin nvarchar(1000) = '" + SkinRealismNegativePrompt + "';\n" +
                "DECLARE @quality nvarchar(1000) = '" + DefaultQualityNegativePrompt + "';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean neutral background, direct eye contact, warm confident smile, soft diffused daylight, natural skin texture, minimal retouching, sharp focus, high-end portrait photography, head-and-shoulders framing',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', hoodie, t-shirt, tank top, athletic wear, sunglasses, hat, messy hair, cluttered background, dramatic lighting, harsh shadows, neon lighting, cyberpunk, synthwave, fashion editorial, glamour makeup, nightclub, beach, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'linkedin';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (crewneck sweater or open-collar shirt, no tie), modern minimalist office or studio background with subtle blurred monitors, confident approachable expression, clean contemporary color palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', suit and tie, tuxedo, hoodie, boardroom, courthouse, doctor coat, medical scrubs, traditional corporate headshot, outdated office, cluttered background, neon lighting, cyberpunk, synthwave, heavy color gels, beach, nightclub, influencer glam, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'tech-professional';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie/crewneck or casual jacket, no suit), energetic approachable vibe, coworking space or creative workspace background, candid smile, relaxed posture, warm natural window light, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beach, nightclub, neon cyberpunk, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'startup';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, elevated smart casual (tailored blazer without tie or premium knit), confident modern presence, upscale modern workspace or cafe background, open relaxed posture, subtle candid energy, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', suit and tie, corporate boardroom, stiff studio headshot, conservative law firm vibe, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, heavy color gels, arms crossed, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'entrepreneur';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, casual lifestyle portrait of {gender} {ethnicity}, everyday clothing (t-shirt/henley/hoodie or sweater, no blazer), relaxed candid smile or laugh, outdoors (park/city street) or cozy home background, golden-hour natural light, relaxed posture (hands in pockets or open gesture), natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'casual';\n");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalLinkedInPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalLinkedInNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'linkedin';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalTechProfessionalPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalTechProfessionalNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'tech-professional';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalStartupPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalStartupNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'startup';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalEntrepreneurPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalEntrepreneurNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'entrepreneur';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalCasualPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalCasualNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'casual';\n");
        }
    }
}
