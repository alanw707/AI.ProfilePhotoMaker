using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFunLifestyleStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var updateTime = new DateTime(2025, 9, 4, 22, 17, 33, 383, DateTimeKind.Utc);

            // Update Style ID 1: Corporate → Beach Vibes
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "beach-vibes", 
                    "Sun-kissed vacation mode portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, subtle beach vacation aesthetic, sun-kissed healthy glow, soft coastal background with blurred ocean hints, warm golden hour lighting, relaxed confident expression, casual summer style, natural beachy hair texture",
                    "winter clothes, cold weather, indoor office, formal business attire, pale skin",
                    updateTime.AddTicks(6786)
                });

            // Update Style ID 3: Consultant → Fresh
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "fresh", 
                    "Clean energetic refreshed portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, fresh clean aesthetic, dewy glowing skin, bright airy background, soft natural lighting, energetic refreshed expression, subtle wet hair effect, spa-like atmosphere, healthy vitality",
                    "tired, exhausted, dull skin, dark shadows, heavy makeup, messy appearance",
                    updateTime.AddTicks(6792)
                });

            // Update Style ID 5: Legal → Retro Wave
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "retro-wave", 
                    "Modern 80s synthwave aesthetic portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, modern retro aesthetic, subtle 80s inspired lighting, soft neon accent colors pink and blue, urban night background, confident trendy expression, contemporary style with vintage flair, cinematic color grading",
                    "modern minimalist, black and white, corporate, traditional, plain background",
                    updateTime.AddTicks(6796)
                });

            // Update Style ID 7: Author → Night Out
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "night-out", 
                    "Sophisticated evening portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, elegant evening aesthetic, warm ambient lighting with bokeh, sophisticated night out look, soft city lights background, confident social expression, subtle glamour, golden hour meets blue hour atmosphere",
                    "casual daywear, bright daylight, morning light, workout clothes, plain appearance",
                    updateTime.AddTicks(6800)
                });

            // Update Style ID 20: Spiritual → Digital Native
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "digital-native", 
                    "Modern tech creator portrait",
                    "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality",
                    "outdated technology, old fashioned, formal business, analog aesthetic, traditional office",
                    updateTime.AddTicks(6874)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore original style values from previous migration

            // Restore Style ID 1: Beach Vibes → Corporate
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "corporate", 
                    "Professional corporate headshot style",
                    "professional corporate headshot, business attire, clean background, confident expression, high-quality photography",
                    "casual clothes, blurred, low quality, unprofessional",
                    new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4341)
                });

            // Restore Style ID 3: Fresh → Consultant
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "consultant", 
                    "Professional consultant style",
                    "professional consultant portrait, business consulting style, smart casual attire, approachable yet professional",
                    "too casual, unprofessional, blurred",
                    new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4347)
                });

            // Restore Style ID 5: Retro Wave → Legal
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "legal", 
                    "Legal professional portrait",
                    "legal professional portrait, formal business attire, trustworthy appearance, professional law office style",
                    "casual, informal, unprofessional",
                    new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4351)
                });

            // Restore Style ID 7: Night Out → Author
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "author", 
                    "Author and writer portrait",
                    "author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression",
                    "unprofessional, distracting elements, poor composition",
                    new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4355)
                });

            // Restore Style ID 20: Digital Native → Spiritual
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    "spiritual", 
                    "Spiritual wellness style",
                    "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic",
                    "materialistic look, stressed appearance, conventional business",
                    new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4383)
                });
        }
    }
}
