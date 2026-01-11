using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLinkedInExecutiveAcademicPrompts : Migration
    {
        private const string OriginalExecutivePromptTemplate =
            "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string OriginalExecutiveNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string OriginalLinkedInPromptTemplate =
            "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), muted professional backdrop with soft gradient (light blue, warm beige, or soft teal), direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus";

        private const string OriginalLinkedInNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, plain white background, plain gray background, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string OriginalAcademicPromptTemplate =
            "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string OriginalAcademicNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string UpdatedExecutivePromptTemplate =
            "{subject}, executive leadership portrait of {gender} {ethnicity}, formal tailored suit with crisp shirt and tie, executive suite or boardroom with skyline windows, composed authoritative expression, confident posture, subtle 3/4 angle, polished professional lighting with controlled contrast, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string UpdatedExecutiveNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, business casual, open collar, coworking space, cafe, classroom, lecture hall, library, bookshelves, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string UpdatedLinkedInPromptTemplate =
            "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, conservative business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft gradient in light blue, warm beige, or soft teal, a clean off-white or warm gray studio backdrop, or a minimal modern office interior, no outdoor setting, no library, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus";

        private const string UpdatedLinkedInNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, outdoor, park, city street, campus, library, bookshelves, lecture hall, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private const string UpdatedAcademicPromptTemplate =
            "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer, cardigan, or academic regalia detail), university library stacks or lecture hall background, scholarly atmosphere, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution";

        private const string UpdatedAcademicNegativePromptTemplate =
            "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles";

        private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var linkedInPrompt = EscapeSqlLiteral(UpdatedLinkedInPromptTemplate);
            var linkedInNegative = EscapeSqlLiteral(UpdatedLinkedInNegativePromptTemplate);
            var executivePrompt = EscapeSqlLiteral(UpdatedExecutivePromptTemplate);
            var executiveNegative = EscapeSqlLiteral(UpdatedExecutiveNegativePromptTemplate);
            var academicPrompt = EscapeSqlLiteral(UpdatedAcademicPromptTemplate);
            var academicNegative = EscapeSqlLiteral(UpdatedAcademicNegativePromptTemplate);

            migrationBuilder.Sql($@"
UPDATE dbo.Styles
SET PromptTemplate = '{linkedInPrompt}',
    NegativePromptTemplate = '{linkedInNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'linkedin';

UPDATE dbo.Styles
SET PromptTemplate = '{executivePrompt}',
    NegativePromptTemplate = '{executiveNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'executive';

UPDATE dbo.Styles
SET PromptTemplate = '{academicPrompt}',
    NegativePromptTemplate = '{academicNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'academic';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var linkedInPrompt = EscapeSqlLiteral(OriginalLinkedInPromptTemplate);
            var linkedInNegative = EscapeSqlLiteral(OriginalLinkedInNegativePromptTemplate);
            var executivePrompt = EscapeSqlLiteral(OriginalExecutivePromptTemplate);
            var executiveNegative = EscapeSqlLiteral(OriginalExecutiveNegativePromptTemplate);
            var academicPrompt = EscapeSqlLiteral(OriginalAcademicPromptTemplate);
            var academicNegative = EscapeSqlLiteral(OriginalAcademicNegativePromptTemplate);

            migrationBuilder.Sql($@"
UPDATE dbo.Styles
SET PromptTemplate = '{linkedInPrompt}',
    NegativePromptTemplate = '{linkedInNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'linkedin';

UPDATE dbo.Styles
SET PromptTemplate = '{executivePrompt}',
    NegativePromptTemplate = '{executiveNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'executive';

UPDATE dbo.Styles
SET PromptTemplate = '{academicPrompt}',
    NegativePromptTemplate = '{academicNegative}',
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) = 'academic';
");
        }
    }
}
