using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class SoftenSkinRealismConstraints : Migration
    {
        // SOFTENED: Removed "poreless skin, exaggerated wrinkles, overly deep wrinkles" to prevent dry/old appearance
        private const string SkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity";

        private const string DefaultQualityNegativePrompt =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands";

        private const string ExpressionAccessoryPoseNegativePrompt =
            "forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions";

        // Previous V4 values for rollback
        private const string PreviousSkinRealismNegativePrompt =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        // UPDATED PROMPTS: Added "healthy natural skin, even skin tone" for better skin quality
        private const string UpdatedAcademicPromptTemplate =
            "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string UpdatedLinkedInPromptTemplate =
            "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus";

        private const string UpdatedStartupPromptTemplate =
            "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string UpdatedTechProfessionalPromptTemplate =
            "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string UpdatedEntrepreneurPromptTemplate =
            "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string UpdatedExecutivePromptTemplate =
            "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        // Previous V4 prompts for rollback
        private const string PreviousAcademicPromptTemplate =
            "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string PreviousLinkedInPromptTemplate =
            "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus";

        private const string PreviousStartupPromptTemplate =
            "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string PreviousTechProfessionalPromptTemplate =
            "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string PreviousEntrepreneurPromptTemplate =
            "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string PreviousExecutivePromptTemplate =
            "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        // Style-specific negative segments (unchanged from V4)
        private const string AcademicNegativePromptSegment =
            "corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup";

        private const string LinkedInNegativePromptSegment =
            "hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text";

        private const string StartupNegativePromptSegment =
            "formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text";

        private const string TechProfessionalNegativePromptSegment =
            "suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text";

        private const string EntrepreneurNegativePromptSegment =
            "formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text";

        private const string ExecutiveNegativePromptSegment =
            "hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text";

        private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var academicPrompt = EscapeSqlLiteral(UpdatedAcademicPromptTemplate);
            var linkedInPrompt = EscapeSqlLiteral(UpdatedLinkedInPromptTemplate);
            var startupPrompt = EscapeSqlLiteral(UpdatedStartupPromptTemplate);
            var techPrompt = EscapeSqlLiteral(UpdatedTechProfessionalPromptTemplate);
            var entrepreneurPrompt = EscapeSqlLiteral(UpdatedEntrepreneurPromptTemplate);
            var executivePrompt = EscapeSqlLiteral(UpdatedExecutivePromptTemplate);

            var academicNegative = EscapeSqlLiteral(AcademicNegativePromptSegment);
            var linkedInNegative = EscapeSqlLiteral(LinkedInNegativePromptSegment);
            var startupNegative = EscapeSqlLiteral(StartupNegativePromptSegment);
            var techNegative = EscapeSqlLiteral(TechProfessionalNegativePromptSegment);
            var entrepreneurNegative = EscapeSqlLiteral(EntrepreneurNegativePromptSegment);
            var executiveNegative = EscapeSqlLiteral(ExecutiveNegativePromptSegment);

            migrationBuilder.Sql($@"
DECLARE @skin nvarchar(1000) = '{EscapeSqlLiteral(SkinRealismNegativePrompt)}';
DECLARE @quality nvarchar(1000) = '{EscapeSqlLiteral(DefaultQualityNegativePrompt)}';
DECLARE @realism nvarchar(1200) = '{EscapeSqlLiteral(ExpressionAccessoryPoseNegativePrompt)}';

UPDATE dbo.Styles
SET PromptTemplate = '{academicPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {academicNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'academic' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{linkedInPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {linkedInNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'linkedin' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{startupPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {startupNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'startup' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{techPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {techNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'tech-professional' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{entrepreneurPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {entrepreneurNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'entrepreneur' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{executivePrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {executiveNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'executive' AND IsActive = 1;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var academicPrompt = EscapeSqlLiteral(PreviousAcademicPromptTemplate);
            var linkedInPrompt = EscapeSqlLiteral(PreviousLinkedInPromptTemplate);
            var startupPrompt = EscapeSqlLiteral(PreviousStartupPromptTemplate);
            var techPrompt = EscapeSqlLiteral(PreviousTechProfessionalPromptTemplate);
            var entrepreneurPrompt = EscapeSqlLiteral(PreviousEntrepreneurPromptTemplate);
            var executivePrompt = EscapeSqlLiteral(PreviousExecutivePromptTemplate);

            var academicNegative = EscapeSqlLiteral(AcademicNegativePromptSegment);
            var linkedInNegative = EscapeSqlLiteral(LinkedInNegativePromptSegment);
            var startupNegative = EscapeSqlLiteral(StartupNegativePromptSegment);
            var techNegative = EscapeSqlLiteral(TechProfessionalNegativePromptSegment);
            var entrepreneurNegative = EscapeSqlLiteral(EntrepreneurNegativePromptSegment);
            var executiveNegative = EscapeSqlLiteral(ExecutiveNegativePromptSegment);

            migrationBuilder.Sql($@"
DECLARE @skin nvarchar(1000) = '{EscapeSqlLiteral(PreviousSkinRealismNegativePrompt)}';
DECLARE @quality nvarchar(1000) = '{EscapeSqlLiteral(DefaultQualityNegativePrompt)}';
DECLARE @realism nvarchar(1200) = '{EscapeSqlLiteral(ExpressionAccessoryPoseNegativePrompt)}';

UPDATE dbo.Styles
SET PromptTemplate = '{academicPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {academicNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'academic' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{linkedInPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {linkedInNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'linkedin' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{startupPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {startupNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'startup' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{techPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {techNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'tech-professional' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{entrepreneurPrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {entrepreneurNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'entrepreneur' AND IsActive = 1;

UPDATE dbo.Styles
SET PromptTemplate = '{executivePrompt}',
    NegativePromptTemplate = CONCAT(@quality, ', {executiveNegative}, ', @realism, ', ', @skin),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'executive' AND IsActive = 1;
");
        }
    }
}
