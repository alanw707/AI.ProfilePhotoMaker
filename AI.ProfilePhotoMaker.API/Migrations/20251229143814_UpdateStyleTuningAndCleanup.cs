using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStyleTuningAndCleanup : Migration
    {
        private const string OriginalRetroPromptTemplate =
            "{subject}, professional portrait of {gender} {ethnicity}, modern retro aesthetic, subtle 80s inspired lighting, soft neon accent colors pink and blue, urban night background, confident trendy expression, contemporary style with vintage flair, cinematic color grading";

        private const string OriginalRetroNegativePromptTemplate =
            "modern minimalist, black and white, corporate, traditional, plain background";

        private const string OriginalNightOutPromptTemplate =
            "{subject}, professional portrait of {gender} {ethnicity}, elegant evening aesthetic, warm ambient lighting with bokeh, sophisticated night out look, soft city lights background, confident social expression, subtle glamour, golden hour meets blue hour atmosphere";

        private const string OriginalNightOutNegativePromptTemplate =
            "casual daywear, bright daylight, morning light, workout clothes, plain appearance";

        private const string OriginalEntrepreneurPromptTemplate =
            "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string OriginalEntrepreneurNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string UpdatedRetroPromptTemplate =
            "{subject}, professional portrait of {gender} {ethnicity}, modern retro wave aesthetic, neon city night backdrop with soft pink and blue accents, soft diffused key light with balanced fill, natural skin tones, realistic skin texture, subtle rim light, shallow depth of field, neon bokeh background, 85mm lens, cinematic color grading, confident trendy expression, contemporary style with vintage flair, subtle film grain";

        private const string UpdatedRetroNegativePromptTemplate =
            "harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened";

        private const string UpdatedNightOutPromptTemplate =
            "{subject}, professional portrait of {gender} {ethnicity}, sophisticated night-out aesthetic, evening city lounge or street bokeh background, ambient street lighting only, soft side key light with gentle fill, no frontal key, no flash, natural skin tones, realistic skin texture, subtle rim light, cinematic color grading, confident social expression, subtle glamour, shallow depth of field, 85mm lens, subtle film grain";

        private const string UpdatedNightOutNegativePromptTemplate =
            "harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance";

        private const string UpdatedEntrepreneurPromptTemplate =
            "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, smart casual wardrobe (open-collar shirt or premium knit, no tie, no formal suit), boutique office, creative studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field";

        private const string UpdatedEntrepreneurNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string SpiritualPromptTemplate =
            "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic";

        private const string SpiritualNegativePromptTemplate =
            "materialistic look, stressed appearance, conventional business";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE dbo.Styles
SET PromptTemplate = '{UpdatedRetroPromptTemplate}',
    NegativePromptTemplate = '{UpdatedRetroNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'retro-wave';

UPDATE dbo.Styles
SET PromptTemplate = '{UpdatedNightOutPromptTemplate}',
    NegativePromptTemplate = '{UpdatedNightOutNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'night-out';

UPDATE dbo.Styles
SET PromptTemplate = '{UpdatedEntrepreneurPromptTemplate}',
    NegativePromptTemplate = '{UpdatedEntrepreneurNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'entrepreneur';

DELETE FROM dbo.Styles WHERE Name = 'spiritual';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE dbo.Styles
SET PromptTemplate = '{OriginalRetroPromptTemplate}',
    NegativePromptTemplate = '{OriginalRetroNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'retro-wave';

UPDATE dbo.Styles
SET PromptTemplate = '{OriginalNightOutPromptTemplate}',
    NegativePromptTemplate = '{OriginalNightOutNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'night-out';

UPDATE dbo.Styles
SET PromptTemplate = '{OriginalEntrepreneurPromptTemplate}',
    NegativePromptTemplate = '{OriginalEntrepreneurNegativePromptTemplate}',
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1 AND Name = 'entrepreneur';

IF NOT EXISTS (SELECT 1 FROM dbo.Styles WHERE Name = 'spiritual')
BEGIN
    INSERT INTO dbo.Styles (Name, Description, PromptTemplate, NegativePromptTemplate, IsActive, CreatedAt, UpdatedAt)
    VALUES ('spiritual', 'Spiritual wellness style', '{SpiritualPromptTemplate}', '{SpiritualNegativePromptTemplate}', 1, GETUTCDATE(), GETUTCDATE());
END
");
        }
    }
}
