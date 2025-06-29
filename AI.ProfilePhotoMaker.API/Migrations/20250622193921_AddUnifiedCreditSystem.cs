using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedCreditSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PremiumPackages",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PremiumPackages",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PremiumPackages",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "PremiumPackages",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "basic-plan");

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-monthly");

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "premium-yearly");

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-monthly");

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: "pro-yearly");

            migrationBuilder.AddColumn<int>(
                name: "PurchasedCredits",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusCredits = table.Column<int>(type: "INTEGER", nullable: false),
                    StripeProductId = table.Column<string>(type: "TEXT", nullable: true),
                    StripePriceId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreditsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BonusCredits", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2025, 6, 22, 19, 39, 20, 638, DateTimeKind.Utc).AddTicks(2749), 50, "Perfect for trying out custom training and styled generations", 1, true, "Starter Pack", 9.99m, null, null, null },
                    { 2, 30, new DateTime(2025, 6, 22, 19, 39, 20, 638, DateTimeKind.Utc).AddTicks(2752), 120, "Most popular - great for professionals needing multiple styles", 2, true, "Professional Pack", 19.99m, null, null, null },
                    { 3, 100, new DateTime(2025, 6, 22, 19, 39, 20, 638, DateTimeKind.Utc).AddTicks(2754), 300, "Best value for content creators and businesses", 3, true, "Studio Pack", 39.99m, null, null, null },
                    { 4, 250, new DateTime(2025, 6, 22, 19, 39, 20, 638, DateTimeKind.Utc).AddTicks(2757), 750, "Maximum credits for agencies and heavy users", 4, true, "Enterprise Pack", 79.99m, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_Name",
                table: "CreditPackages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_PackageId",
                table: "CreditPurchases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId",
                table: "CreditPurchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditPurchases");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropColumn(
                name: "PurchasedCredits",
                table: "UserProfiles");

            migrationBuilder.InsertData(
                table: "PremiumPackages",
                columns: new[] { "Id", "CreatedAt", "Credits", "Description", "IsActive", "MaxImagesPerStyle", "MaxStyles", "Name", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8974), 5, "Generate up to 4 professional photos with 2 different styles", true, 2, 2, "Quick Shot", 9.99m, null },
                    { 2, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8976), 15, "Generate up to 14 professional photos with 5 different styles", true, 3, 5, "Professional", 19.99m, null },
                    { 3, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8978), 35, "Generate up to 34 professional photos with 8 different styles", true, 4, 8, "Premium Studio", 34.99m, null },
                    { 4, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8980), 50, "Generate up to 49 professional photos with 10 different styles", true, 5, 10, "Ultimate", 49.99m, null }
                });

            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8697), "Professional headshot with corporate styling", true, "professional", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, t-shirt, vacation setting, party scene, inappropriate attire", "{subject}, professional headshot, corporate portrait style, composition: centered subject with neutral background, slight angle, lighting: three-point studio lighting with soft key light, fill light, and rim light, color palette: muted blues and grays with natural skin tones, mood: confident and approachable, technical details: shot with 85mm lens at f/2.8, shallow depth of field, 4K resolution, additional elements: subtle office or gradient background, professional attire, well-groomed appearance", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8697) },
                    { 2, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8700), "Relaxed lifestyle portrait with natural styling", true, "casual", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", "{subject}, casual lifestyle portrait, composition: rule of thirds with natural framing, lighting: golden hour natural sunlight with soft diffusion, color palette: warm earthy tones with vibrant accents, mood: relaxed, friendly and authentic, technical details: shot with 50mm lens at f/2.0, medium depth of field, additional elements: outdoor setting with natural elements, casual stylish clothing, genuine smile", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8701) },
                    { 3, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8703), "Artistic creative portrait with dynamic composition", true, "creative", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, boring, plain background, standard pose, conventional lighting", "{subject}, artistic creative portrait, composition: dynamic asymmetrical framing with creative negative space, lighting: dramatic side lighting with colored gels and intentional shadows, color palette: bold contrasting colors with artistic color grading, mood: intriguing and expressive, technical details: shot with wide angle lens, creative perspective, high contrast, additional elements: artistic background elements, creative props or styling, unique fashion elements", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8703) },
                    { 4, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8705), "Executive corporate portrait with formal styling", true, "corporate", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual attire, beach, party scene, inappropriate setting", "{subject}, executive corporate portrait, composition: formal centered composition with professional framing, lighting: classic Rembrandt lighting with soft fill, color palette: deep blues, grays and blacks with subtle accents, mood: authoritative, trustworthy and professional, technical details: shot with medium telephoto lens, optimal clarity and sharpness, additional elements: elegant business attire, office or branded environment subtly visible, power posture", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8705) },
                    { 5, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8707), "Optimized LinkedIn profile photo with professional appeal", true, "linkedin", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, full body shot, distracting background, extreme filters, unprofessional setting", "{subject}, optimized LinkedIn profile photo, composition: head and shoulders framing with balanced negative space above head, lighting: flattering soft light with subtle highlighting, color palette: professional neutral tones with complementary background, mood: approachable yet professional, technical details: 1000x1000 pixel square format, sharp focus on eyes, additional elements: simple clean background, professional but approachable expression, business casual attire", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8707) },
                    { 6, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8709), "Scholarly academic portrait with intellectual elements", true, "academic", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", "{subject}, scholarly academic portrait, composition: dignified framing with intellectual elements, lighting: soft even lighting with subtle gradient, color palette: rich traditional tones with subtle depth, mood: thoughtful, knowledgeable and authoritative, technical details: medium format quality, excellent clarity, additional elements: books, laboratory or campus environment, academic attire or professional clothing, scholarly posture", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8709) },
                    { 7, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8711), "Modern tech industry portrait with contemporary styling", true, "tech", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated technology, traditional office, formal suit", "{subject}, modern tech industry portrait, composition: contemporary framing with technical elements, lighting: modern high-key lighting with subtle blue accents, color palette: tech blues and cool grays with vibrant accents, mood: innovative, forward-thinking and approachable, technical details: ultra-high definition, perfect clarity, additional elements: minimal tech environment, modern casual professional attire, confident engaged expression", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8711) },
                    { 8, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8714), "Healthcare professional portrait with trustworthy appeal", true, "medical", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, inappropriate medical setting, casual vacation clothing", "{subject}, healthcare professional portrait, composition: trustworthy frontal composition with medical context, lighting: clean even lighting with healthy glow, color palette: whites, blues and comforting tones, mood: compassionate, competent and reassuring, technical details: sharp focus throughout, excellent clarity, additional elements: medical attire or lab coat, stethoscope or medical environment, caring expression", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8714) },
                    { 9, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8716), "Legal professional portrait with authoritative presence", true, "legal", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, inappropriate attire, party scene", "{subject}, legal professional portrait, composition: balanced formal composition with legal elements, lighting: classical portrait lighting with defined shadows, color palette: deep rich tones with mahogany and navy accents, mood: authoritative, trustworthy and dignified, technical details: perfect focus and formal composition, additional elements: legal books, office with wooden elements, formal suit, confident and serious expression", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8716) },
                    { 10, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8718), "Premium executive portrait with commanding presence", true, "executive", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, unprofessional setting, low quality office", "{subject}, premium executive portrait, composition: powerful centered composition with prestigious elements, lighting: dramatic executive lighting with defined highlights, color palette: luxury tones with gold, navy and charcoal accents, mood: powerful, successful and commanding, technical details: medium format quality with perfect detail rendering, additional elements: luxury office environment, premium suit or executive attire, leadership pose and expression", new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8718) }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "BillingPeriod", "CanBatchGenerate", "CanTrainCustomModels", "CreatedAt", "Description", "HighResolutionOutput", "ImagesPerMonth", "IsActive", "MaxStylesAccess", "MaxTrainingImages", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[,]
                {
                    { "basic-plan", "monthly", false, false, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8933), "Perfect for casual users who want to enhance their photos", false, 3, true, 1, 0, "Basic", 0.00m, null, null, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8933) },
                    { "premium-monthly", "monthly", true, true, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8938), "Ideal for professionals who need custom AI models and advanced features", true, 50, true, 5, 20, "Premium", 19.99m, null, null, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8939) },
                    { "premium-yearly", "yearly", true, true, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8941), "Premium features with 2 months free when billed annually", true, 50, true, 5, 20, "Premium (Yearly)", 199.99m, null, null, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8941) },
                    { "pro-monthly", "monthly", true, true, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8944), "For businesses and power users who need unlimited access", true, 200, true, 10, 50, "Pro", 49.99m, null, null, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8944) },
                    { "pro-yearly", "yearly", true, true, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8946), "Pro features with 2 months free when billed annually", true, 200, true, 10, 50, "Pro (Yearly)", 499.99m, null, null, new DateTime(2025, 6, 20, 13, 0, 26, 814, DateTimeKind.Utc).AddTicks(8947) }
                });
        }
    }
}
