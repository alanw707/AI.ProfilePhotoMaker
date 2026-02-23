using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <summary>
    /// Restores the Academic style (Id=18) if it was deactivated or removed during today's migrations.
    /// Safe: only touches the academic row, does not modify any other styles.
    /// </summary>
    public partial class RestoreAcademicStyle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Styles] WHERE [Id] = 18)
BEGIN
    -- Row exists but may be inactive or have wrong name — restore it
    UPDATE [Styles]
    SET [Name] = 'academic',
        [Description] = 'Academic professional style',
        [PromptTemplate] = '{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',
        [NegativePromptTemplate] = 'blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed',
        [IsActive] = 1,
        [UpdatedAt] = GETUTCDATE()
    WHERE [Id] = 18;
END
ELSE
BEGIN
    -- Row is missing entirely — insert it
    INSERT INTO [Styles] ([Id], [Name], [Description], [PromptTemplate], [NegativePromptTemplate], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (
        18,
        'academic',
        'Academic professional style',
        '{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution',
        'blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed',
        1,
        GETUTCDATE(),
        GETUTCDATE()
    );
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Safe rollback: deactivate rather than delete to preserve referential integrity
            migrationBuilder.Sql(@"
UPDATE [Styles] SET [IsActive] = 0, [UpdatedAt] = GETUTCDATE() WHERE [Id] = 18;
");
        }
    }
}
