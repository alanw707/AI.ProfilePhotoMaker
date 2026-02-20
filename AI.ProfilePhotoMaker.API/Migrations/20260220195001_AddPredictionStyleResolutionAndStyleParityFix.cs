using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    public partial class AddPredictionStyleResolutionAndStyleParityFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedStyle",
                table: "Predictions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedStyle",
                table: "Predictions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedStyleId",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ResolvedStyleId",
                table: "Predictions",
                column: "ResolvedStyleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_Styles_ResolvedStyleId",
                table: "Predictions",
                column: "ResolvedStyleId",
                principalTable: "Styles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Strengthen casual clothing guardrails and fitness anti-cross-style terms.
            migrationBuilder.Sql(@"
UPDATE [Styles]
SET [PromptTemplate] = '{subject}, casual lifestyle portrait of {gender} {ethnicity}, wearing a casual t-shirt or henley or hoodie or crewneck sweater (fully clothed upper body), relaxed candid smile or laugh, outdoors (park/city street) or cozy home background, golden-hour natural light, relaxed posture (hands in pockets or open gesture), natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field',
    [NegativePromptTemplate] = 'blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, bare torso, topless, no shirt, nude, undressed, exposed skin, skin showing, without shirt, uncovered chest, tank top, undershirt, muscle tank, sleeveless shirt',
    [UpdatedAt] = GETUTCDATE()
WHERE [Name] = 'casual';
");

            migrationBuilder.Sql(@"
UPDATE [Styles]
SET [PromptTemplate] = '{subject}, fitness professional portrait of {gender} {ethnicity}, athletic performance aesthetic, fitted athletic apparel, gym or wellness studio backdrop, energetic confident expression',
    [NegativePromptTemplate] = 'blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, sedentary look, unhealthy appearance, low energy, leather jacket, city street fashion, edgy-urban style, nightclub look, suit and tie, corporate boardroom, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching',
    [UpdatedAt] = GETUTCDATE()
WHERE [Name] = 'fitness';
");

            // Ensure digital-native canonical row is Id=20 and migrate existing references from Id=21.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Styles] WHERE [Id] = 20)
BEGIN
    INSERT INTO [Styles] ([Id], [Name], [Description], [PromptTemplate], [NegativePromptTemplate], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (20, 'digital-native', 'Modern tech creator portrait', '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality', 'blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed', 1, GETUTCDATE(), GETUTCDATE());
END
ELSE
BEGIN
    UPDATE [Styles]
    SET [Name] = 'digital-native',
        [Description] = 'Modern tech creator portrait',
        [PromptTemplate] = '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality',
        [NegativePromptTemplate] = 'blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed',
        [IsActive] = 1,
        [UpdatedAt] = GETUTCDATE()
    WHERE [Id] = 20;
END

IF EXISTS (SELECT 1 FROM [Styles] WHERE [Id] = 21)
BEGIN
    IF OBJECT_ID('UserStyleSelections', 'U') IS NOT NULL
    BEGIN
        UPDATE [UserStyleSelections]
        SET [StyleId] = 20
        WHERE [StyleId] = 21;
    END

    IF OBJECT_ID('UserProfiles', 'U') IS NOT NULL AND COL_LENGTH('UserProfiles', 'StyleId') IS NOT NULL
    BEGIN
        UPDATE [UserProfiles]
        SET [StyleId] = 20
        WHERE [StyleId] = 21;
    END

    DELETE FROM [Styles] WHERE [Id] = 21;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_Styles_ResolvedStyleId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_ResolvedStyleId",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "RequestedStyle",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ResolvedStyle",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ResolvedStyleId",
                table: "Predictions");

            // Keep rollback data-safe: recreate style 21 if missing and remap references back,
            // but do not hard-delete style 20 because it may be referenced in live data.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Styles] WHERE [Id] = 21)
BEGIN
    INSERT INTO [Styles] ([Id], [Name], [Description], [PromptTemplate], [NegativePromptTemplate], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (21, 'digital-native', 'Modern tech creator portrait', '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality', 'blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed', 1, GETUTCDATE(), GETUTCDATE());
END

IF OBJECT_ID('UserStyleSelections', 'U') IS NOT NULL
BEGIN
    UPDATE [UserStyleSelections]
    SET [StyleId] = 21
    WHERE [StyleId] = 20;
END

IF OBJECT_ID('UserProfiles', 'U') IS NOT NULL AND COL_LENGTH('UserProfiles', 'StyleId') IS NOT NULL
BEGIN
    UPDATE [UserProfiles]
    SET [StyleId] = 21
    WHERE [StyleId] = 20;
END
");
        }
    }
}
