using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixStylePromptsDataDriftAndQualityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8739));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8741));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8743));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Description", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8895), "Sun-kissed vacation mode portrait", "beach-vibes", "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, winter clothes, cold weather, indoor office, formal business attire, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", "{subject}, professional portrait of {gender} {ethnicity}, subtle beach vacation aesthetic, sun-kissed healthy glow, soft coastal background with blurred ocean hints, warm golden hour lighting, relaxed confident expression, casual summer style, natural beachy hair texture, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8896) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8899), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8900) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Description", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8901), "Clean energetic refreshed portrait", "fresh", "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, tired, exhausted, dull skin, dark shadows, heavy makeup, messy appearance, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching", "{subject}, professional portrait of {gender} {ethnicity}, fresh clean aesthetic, dewy glowing skin, bright airy background, soft natural lighting, energetic refreshed expression, healthy vitality, natural skin texture, minimal retouching, head-and-shoulders framing", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8902) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8903), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8904) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8905), "harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8906) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8907), "unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8908) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8909), "harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8910) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8911), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8912) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8913), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8915) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8916), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8917) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8918), "formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8919) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8920), "formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8920) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8922), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8922) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8924), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, bare torso, topless, no shirt, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8924) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8926), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8926) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8928), "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, conservative formal, traditional business, bland conventional, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8928) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8931), "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, casual simple, plain appearance, understated look, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8931) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8933), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8933) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8935), "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, sedentary look, unhealthy appearance, low energy, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8935) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8937), "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", new DateTime(2026, 2, 20, 13, 21, 7, 563, DateTimeKind.Utc).AddTicks(8937) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8796));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8800));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8801));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Description", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9002), "Professional corporate headshot style", "corporate", "casual clothes, blurred, low quality, unprofessional, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", "{subject}, professional corporate headshot of {gender} {ethnicity}, business attire, clean background, confident expression, high-quality photography", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9004) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9007), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9007) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Description", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9009), "Professional consultant style", "consultant", "too casual, unprofessional, blurred, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", "{subject}, professional consultant portrait of {gender} {ethnicity}, business consulting style, smart casual attire, approachable yet professional", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9009) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9011), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9011) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9013), "harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9014) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9015), "unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9016) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9018), "harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9019) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9020), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9023), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9025), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9027), "formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9027) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9030), "formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9030) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9032), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9032) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9034), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9034) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9036), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9036) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9038), "conservative formal, traditional business, bland conventional", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9038) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9041), "casual simple, plain appearance, understated look", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9041) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9043), "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9043) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9045), "sedentary look, unhealthy appearance, low energy", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9045) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9047), "outdated technology, old fashioned, formal business, analog aesthetic, traditional office", new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9047) });
        }
    }
}
