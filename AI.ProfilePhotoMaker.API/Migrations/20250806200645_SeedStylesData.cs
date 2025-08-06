using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class SeedStylesData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(3929));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(3935));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(3938));

            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4278), "Professional corporate headshot style", true, "corporate", "casual clothes, blurred, low quality, unprofessional", "professional corporate headshot, business attire, clean background, confident expression, high-quality photography", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4278) },
                    { 2, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4281), "Executive leadership portrait", true, "executive", "casual, informal, poor lighting, unprofessional", "executive portrait, professional leadership style, formal business attire, authoritative presence, studio lighting", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4282) },
                    { 3, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4284), "Professional consultant style", true, "consultant", "too casual, unprofessional, blurred", "professional consultant portrait, business consulting style, smart casual attire, approachable yet professional", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4285) },
                    { 4, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4287), "LinkedIn professional networking", true, "linkedin", "casual clothes, distracting background, unprofessional", "linkedin profile photo, professional networking style, business attire, friendly professional expression, clean background", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4288) },
                    { 5, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4290), "Legal professional portrait", true, "legal", "casual, informal, unprofessional", "legal professional portrait, formal business attire, trustworthy appearance, professional law office style", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4290) },
                    { 6, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4292), "Healthcare professional style", true, "medical", "casual clothes, unprofessional, poor quality", "medical professional portrait, healthcare style, professional medical attire, trustworthy healthcare provider appearance", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4293) },
                    { 7, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4295), "Author and writer portrait", true, "author", "unprofessional, distracting elements, poor composition", "author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4295) },
                    { 8, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4297), "Entrepreneurial business style", true, "entrepreneur", "formal corporate look, traditional, static pose", "entrepreneur portrait, innovative business leader style, modern professional attire, dynamic confident expression", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4298) },
                    { 9, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4300), "Startup professional style", true, "startup", "overly formal, traditional corporate, stiff pose", "startup professional portrait, innovative tech style, modern casual business attire, entrepreneurial spirit", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4301) },
                    { 10, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4303), "Technology professional style", true, "tech-professional", "overly formal, outdated style, unprofessional", "tech professional portrait, modern technology industry style, smart casual tech attire, innovative professional look", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4303) },
                    { 11, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4305), "Social media influencer style", true, "influencer", "overly formal, corporate look, boring expression", "social media influencer portrait, engaging personality style, trendy professional appearance, charismatic expression", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4305) },
                    { 12, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4308), "Digital nomad professional", true, "digital-nomad", "formal office attire, traditional corporate, static background", "digital nomad portrait, remote work professional style, casual modern attire, location-independent professional", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4308) },
                    { 13, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4310), "Creative professional style", true, "creative", "corporate formal, traditional business, boring conventional look", "creative professional portrait, artistic style, expressive creative look, innovative artistic appearance", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4311) },
                    { 14, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4312), "Casual professional style", true, "casual", "overly formal, stiff corporate, too dressy", "casual professional portrait, relaxed business style, smart casual attire, approachable friendly appearance", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4313) },
                    { 15, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4315), "Artistic creative portrait", true, "artistic", "corporate business, formal attire, conventional look", "artistic portrait, creative artistic style, expressive artistic look, bohemian creative appearance", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4315) },
                    { 16, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4317), "Edgy urban style", true, "edgy-urban", "conservative formal, traditional business, bland conventional", "edgy urban portrait, modern urban style, contemporary city fashion, bold confident expression", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4318) },
                    { 17, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4320), "Glamour portrait style", true, "glamour", "casual simple, plain appearance, understated look", "glamour portrait, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4320) },
                    { 18, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4323), "Academic professional style", true, "academic", "casual informal, unprofessional, non-academic", "academic portrait, scholarly professional style, intellectual appearance, educational professional look", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4324) },
                    { 19, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4327), "Fitness professional style", true, "fitness", "sedentary look, unhealthy appearance, low energy", "fitness professional portrait, athletic style, health and wellness appearance, energetic confident look", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4328) },
                    { 20, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4330), "Spiritual wellness style", true, "spiritual", "materialistic look, stressed appearance, conventional business", "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4331) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 5, 26, 52, 534, DateTimeKind.Utc).AddTicks(2396));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 5, 26, 52, 534, DateTimeKind.Utc).AddTicks(2398));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 5, 26, 52, 534, DateTimeKind.Utc).AddTicks(2400));
        }
    }
}
