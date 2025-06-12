using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStylesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StyleId",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Styles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PromptTemplate = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    NegativePromptTemplate = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Styles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7705), "Professional headshot with corporate styling", true, "professional", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, t-shirt, vacation setting, party scene, inappropriate attire", "{subject}, professional headshot, corporate portrait style, composition: centered subject with neutral background, slight angle, lighting: three-point studio lighting with soft key light, fill light, and rim light, color palette: muted blues and grays with natural skin tones, mood: confident and approachable, technical details: shot with 85mm lens at f/2.8, shallow depth of field, 4K resolution, additional elements: subtle office or gradient background, professional attire, well-groomed appearance", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7705) },
                    { 2, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7708), "Relaxed lifestyle portrait with natural styling", true, "casual", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", "{subject}, casual lifestyle portrait, composition: rule of thirds with natural framing, lighting: golden hour natural sunlight with soft diffusion, color palette: warm earthy tones with vibrant accents, mood: relaxed, friendly and authentic, technical details: shot with 50mm lens at f/2.0, medium depth of field, additional elements: outdoor setting with natural elements, casual stylish clothing, genuine smile", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7708) },
                    { 3, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7710), "Artistic creative portrait with dynamic composition", true, "creative", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, boring, plain background, standard pose, conventional lighting", "{subject}, artistic creative portrait, composition: dynamic asymmetrical framing with creative negative space, lighting: dramatic side lighting with colored gels and intentional shadows, color palette: bold contrasting colors with artistic color grading, mood: intriguing and expressive, technical details: shot with wide angle lens, creative perspective, high contrast, additional elements: artistic background elements, creative props or styling, unique fashion elements", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7710) },
                    { 4, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7712), "Executive corporate portrait with formal styling", true, "corporate", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual attire, beach, party scene, inappropriate setting", "{subject}, executive corporate portrait, composition: formal centered composition with professional framing, lighting: classic Rembrandt lighting with soft fill, color palette: deep blues, grays and blacks with subtle accents, mood: authoritative, trustworthy and professional, technical details: shot with medium telephoto lens, optimal clarity and sharpness, additional elements: elegant business attire, office or branded environment subtly visible, power posture", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7712) },
                    { 5, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7714), "Optimized LinkedIn profile photo with professional appeal", true, "linkedin", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, full body shot, distracting background, extreme filters, unprofessional setting", "{subject}, optimized LinkedIn profile photo, composition: head and shoulders framing with balanced negative space above head, lighting: flattering soft light with subtle highlighting, color palette: professional neutral tones with complementary background, mood: approachable yet professional, technical details: 1000x1000 pixel square format, sharp focus on eyes, additional elements: simple clean background, professional but approachable expression, business casual attire", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7714) },
                    { 6, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7716), "Scholarly academic portrait with intellectual elements", true, "academic", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", "{subject}, scholarly academic portrait, composition: dignified framing with intellectual elements, lighting: soft even lighting with subtle gradient, color palette: rich traditional tones with subtle depth, mood: thoughtful, knowledgeable and authoritative, technical details: medium format quality, excellent clarity, additional elements: books, laboratory or campus environment, academic attire or professional clothing, scholarly posture", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7716) },
                    { 7, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7718), "Modern tech industry portrait with contemporary styling", true, "tech", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated technology, traditional office, formal suit", "{subject}, modern tech industry portrait, composition: contemporary framing with technical elements, lighting: modern high-key lighting with subtle blue accents, color palette: tech blues and cool grays with vibrant accents, mood: innovative, forward-thinking and approachable, technical details: ultra-high definition, perfect clarity, additional elements: minimal tech environment, modern casual professional attire, confident engaged expression", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7718) },
                    { 8, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7720), "Healthcare professional portrait with trustworthy appeal", true, "medical", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, inappropriate medical setting, casual vacation clothing", "{subject}, healthcare professional portrait, composition: trustworthy frontal composition with medical context, lighting: clean even lighting with healthy glow, color palette: whites, blues and comforting tones, mood: compassionate, competent and reassuring, technical details: sharp focus throughout, excellent clarity, additional elements: medical attire or lab coat, stethoscope or medical environment, caring expression", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7720) },
                    { 9, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8848), "Legal professional portrait with authoritative presence", true, "legal", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, inappropriate attire, party scene", "{subject}, legal professional portrait, composition: balanced formal composition with legal elements, lighting: classical portrait lighting with defined shadows, color palette: deep rich tones with mahogany and navy accents, mood: authoritative, trustworthy and dignified, technical details: perfect focus and formal composition, additional elements: legal books, office with wooden elements, formal suit, confident and serious expression", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8849) },
                    { 10, new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8851), "Premium executive portrait with commanding presence", true, "executive", "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, unprofessional setting, low quality office", "{subject}, premium executive portrait, composition: powerful centered composition with prestigious elements, lighting: dramatic executive lighting with defined highlights, color palette: luxury tones with gold, navy and charcoal accents, mood: powerful, successful and commanding, technical details: medium format quality with perfect detail rendering, additional elements: luxury office environment, premium suit or executive attire, leadership pose and expression", new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8851) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_StyleId",
                table: "UserProfiles",
                column: "StyleId");

            migrationBuilder.CreateIndex(
                name: "IX_Styles_Name",
                table: "Styles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Styles_StyleId",
                table: "UserProfiles",
                column: "StyleId",
                principalTable: "Styles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Styles_StyleId",
                table: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Styles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_StyleId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "StyleId",
                table: "UserProfiles");
        }
    }
}
