using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260220221000_RestoreAcademicStyleIfMissing")]
    public partial class RestoreAcademicStyleIfMissing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = GETUTCDATE();

IF EXISTS (SELECT 1 FROM [Styles] WHERE [Name] = 'academic')
BEGIN
    UPDATE [Styles]
    SET [Description] = 'Academic professional style',
        [PromptTemplate] = '{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',
        [NegativePromptTemplate] = 'blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed',
        [IsActive] = 1,
        [UpdatedAt] = @now
    WHERE [Name] = 'academic';
END
ELSE
BEGIN
    INSERT INTO [Styles] ([Name], [Description], [PromptTemplate], [NegativePromptTemplate], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (
        'academic',
        'Academic professional style',
        '{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',
        'blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed',
        1,
        @now,
        @now
    );
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair migration; no destructive rollback.
        }
    }
}
