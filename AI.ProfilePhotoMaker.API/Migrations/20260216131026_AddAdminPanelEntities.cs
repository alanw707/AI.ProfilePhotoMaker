using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPanelEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxUsages = table.Column<int>(type: "int", nullable: false),
                    CurrentUsages = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByAdminId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CouponRedemptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CouponId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscountApplied = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CouponRedemptions_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8828));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8832));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8834));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8991), "casual clothes, blurred, low quality, unprofessional, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", "{subject}, professional corporate headshot of {gender} {ethnicity}, business attire, clean background, confident expression, high-quality photography", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8992) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8995), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8995) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8997), "too casual, unprofessional, blurred, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", "{subject}, professional consultant portrait of {gender} {ethnicity}, business consulting style, smart casual attire, approachable yet professional", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8997) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8999), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8999) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9001), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9001) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9003), "unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, medical professional portrait of {gender} {ethnicity}, healthcare style, professional medical attire, trustworthy healthcare provider appearance, warm caring expression, clinical background", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9003) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9006), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9006) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9008), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9008) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9010), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9011) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9012), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9012) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9014), "formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, influencer portrait of {gender} {ethnicity}, engaging personality style, trendy professional appearance, charismatic expression", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9014) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9017), "formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, digital nomad portrait of {gender} {ethnicity}, remote work professional style, casual modern attire, location-independent professional", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9017) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9019), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, creative professional portrait of {gender} {ethnicity}, modern creative director vibe, stylish contemporary outfit, subtle colorful studio or gallery background, bright airy daylight, playful confident expression, clean editorial photography, head-and-shoulders framing, natural skin texture, minimal retouching, sharp focus, high-resolution", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9019) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9021), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, casual lifestyle portrait of {gender} {ethnicity}, everyday clothing (t-shirt/henley/hoodie or sweater, no blazer), relaxed candid smile or laugh, outdoors (park/city street) or cozy home background, golden-hour natural light, relaxed posture (hands in pockets or open gesture), natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9023), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, fine-art portrait photography of {gender} {ethnicity}, cinematic moody studio lighting, chiaroscuro, textured backdrop, cinematic color grading, contemplative expression, artistic composition, subtle film grain, head-and-shoulders framing, natural skin texture, minimal retouching, high-resolution", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9025), "{subject}, edgy urban portrait of {gender} {ethnicity}, modern urban style, contemporary city fashion, bold confident expression", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9028), "{subject}, glamour portrait of {gender} {ethnicity}, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9028) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9030), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9030) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9032), "{subject}, fitness professional portrait of {gender} {ethnicity}, athletic style, health and wellness appearance, energetic confident look", new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9032) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9034), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9034) });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminUserId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "AdminUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_CouponId_UserId_Unique",
                table: "CouponRedemptions",
                columns: new[] { "CouponId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code_Unique",
                table: "Coupons",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "CouponRedemptions");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7073));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7076));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7078));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7791), "casual clothes, blurred, low quality, unprofessional", "professional corporate headshot, business attire, clean background, confident expression, high-quality photography", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7792) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7795), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7795) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7800), "too casual, unprofessional, blurred", "professional consultant portrait, business consulting style, smart casual attire, approachable yet professional", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7800) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7803), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7803) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7805), new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7806) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7808), "casual clothes, unprofessional, poor quality", "medical professional portrait, healthcare style, professional medical attire, trustworthy healthcare provider appearance", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7824) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7826), new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7827) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7829), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7829) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7831), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7832) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7834), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7834) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7837), "overly formal, corporate look, boring expression", "social media influencer portrait, engaging personality style, trendy professional appearance, charismatic expression", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7837) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7839), "formal office attire, traditional corporate, static background", "digital nomad portrait, remote work professional style, casual modern attire, location-independent professional", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7839) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7841), "corporate formal, traditional business, boring conventional look", "creative professional portrait, artistic style, expressive creative look, innovative artistic appearance", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7842) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7844), "overly formal, stiff corporate, too dressy", "casual professional portrait, relaxed business style, smart casual attire, approachable friendly appearance", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7844) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7847), "corporate business, formal attire, conventional look", "artistic portrait, creative artistic style, expressive artistic look, bohemian creative appearance", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7847) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7849), "edgy urban portrait, modern urban style, contemporary city fashion, bold confident expression", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7850) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7853), "glamour portrait, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7853) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7855), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7856) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7858), "fitness professional portrait, athletic style, health and wellness appearance, energetic confident look", new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7858) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7860), new DateTime(2026, 1, 11, 5, 17, 13, 652, DateTimeKind.Utc).AddTicks(7861) });
        }
    }
}
