using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProfessionalClusterPromptsV3 : Migration
    {
        private const string SkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string DefaultQualityNegativePrompt =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands";

        private const string ExpressionAccessoryPoseNegativePrompt =
            "forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions";

        private const string OriginalAcademicPromptTemplate =
            "academic portrait, scholarly professional style, intellectual appearance, educational professional look";

        private const string OriginalAcademicNegativePromptTemplate =
            "casual informal, unprofessional, non-academic, " + SkinRealismNegativePrompt;

        private const string OriginalExecutivePromptTemplate =
            "executive portrait, professional leadership style, formal business attire, authoritative presence, studio lighting";

        private const string OriginalExecutiveNegativePromptTemplate =
            "casual, informal, poor lighting, unprofessional, " + SkinRealismNegativePrompt;

        private const string OriginalLinkedInPromptTemplate =
            "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean neutral background, direct eye contact, warm confident smile, soft diffused daylight, natural skin texture, minimal retouching, sharp focus, high-end portrait photography, head-and-shoulders framing";

        private const string OriginalLinkedInNegativePromptTemplate =
            DefaultQualityNegativePrompt + ", hoodie, t-shirt, tank top, athletic wear, sunglasses, hat, messy hair, cluttered background, dramatic lighting, harsh shadows, neon lighting, cyberpunk, synthwave, fashion editorial, glamour makeup, nightclub, beach, full body shot, watermark, text, " + SkinRealismNegativePrompt;

        private const string OriginalTechProfessionalPromptTemplate =
            "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (crewneck sweater or open-collar shirt, no tie), modern minimalist office or studio background with subtle blurred monitors, confident approachable expression, clean contemporary color palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string OriginalTechProfessionalNegativePromptTemplate =
            DefaultQualityNegativePrompt + ", suit and tie, tuxedo, hoodie, boardroom, courthouse, doctor coat, medical scrubs, traditional corporate headshot, outdated office, cluttered background, neon lighting, cyberpunk, synthwave, heavy color gels, beach, nightclub, influencer glam, full body shot, watermark, text, " + SkinRealismNegativePrompt;

        private const string OriginalStartupPromptTemplate =
            "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie/crewneck or casual jacket, no suit), energetic approachable vibe, coworking space or creative workspace background, candid smile, relaxed posture, warm natural window light, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string OriginalStartupNegativePromptTemplate =
            DefaultQualityNegativePrompt + ", formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beach, nightclub, neon cyberpunk, full body shot, watermark, text, " + SkinRealismNegativePrompt;

        private const string OriginalEntrepreneurPromptTemplate =
            "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, elevated smart casual (tailored blazer without tie or premium knit), confident modern presence, upscale modern workspace or cafe background, open relaxed posture, subtle candid energy, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string OriginalEntrepreneurNegativePromptTemplate =
            DefaultQualityNegativePrompt + ", suit and tie, corporate boardroom, stiff studio headshot, conservative law firm vibe, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, heavy color gels, arms crossed, full body shot, watermark, text, " + SkinRealismNegativePrompt;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DECLARE @skin nvarchar(1000) = '" + SkinRealismNegativePrompt + "';\n" +
                "DECLARE @quality nvarchar(1000) = '" + DefaultQualityNegativePrompt + "';\n" +
                "DECLARE @realism nvarchar(1200) = '" + ExpressionAccessoryPoseNegativePrompt + "';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'academic';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), muted professional backdrop with soft gradient (light blue, warm beige, or soft teal), direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', hoodie, t-shirt, tank top, athletic wear, coworking space, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, plain white background, plain gray background, full body shot, watermark, text, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'linkedin';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'startup';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'tech-professional';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'entrepreneur';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',\n" +
                "    NegativePromptTemplate = CONCAT(@quality, ', hoodie, t-shirt, casual streetwear, coworking space, cafe, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, ', @realism, ', ', @skin),\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'executive';\n");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalAcademicPromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalAcademicNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'academic';\n" +
                "\n" +
                "UPDATE dbo.Styles\n" +
                "SET PromptTemplate = '" + OriginalExecutivePromptTemplate + "',\n" +
                "    NegativePromptTemplate = '" + OriginalExecutiveNegativePromptTemplate + "',\n" +
                "    UpdatedAt = GETUTCDATE()\n" +
                "WHERE IsActive = 1 AND Name = 'executive';\n" +
                "\n" +
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
                "WHERE IsActive = 1 AND Name = 'entrepreneur';\n");
        }
    }
}
