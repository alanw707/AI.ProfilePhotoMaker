using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStylePromptsWithTriggerPlaceholder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update all style prompt templates to include {trigger} placeholder for better grammar

            // Professional & Career Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                column: "PromptTemplate",
                value: "Professional studio portrait of {trigger} who is a {gender} in formal business attire, clean background, confident expression, corporate office lighting, sharp focus");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                column: "PromptTemplate",
                value: "High-end executive portrait of {trigger} who is a {gender}, power pose, elegant business suit, luxury office background, natural light, serious expression, premium look");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                column: "PromptTemplate",
                value: "Portrait of {trigger} who is a friendly {gender} consultant in semi-formal smart-casual attire, clean background, approachable expression, modern professional tone");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                column: "PromptTemplate",
                value: "Professional LinkedIn-style headshot of {trigger} who is a {gender}, neutral background, confident and warm smile, clean business-casual attire, high clarity");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                column: "PromptTemplate",
                value: "Formal portrait of {trigger} who is a {gender} lawyer in courtroom or law office, dark tailored suit, serious expression, soft shadows, bookshelf or columns in background");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                column: "PromptTemplate",
                value: "Portrait of {trigger} who is a {gender} healthcare professional in lab coat, stethoscope, hospital or clinic background, calm and trustworthy expression, soft light");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                column: "PromptTemplate",
                value: "Intellectual portrait of {trigger} who is a {gender} with bookshelves or writing desk in the background, warm ambient lighting, thoughtful gaze, creative professional styling");

            // Modern Entrepreneur & Tech Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                column: "PromptTemplate",
                value: "Modern portrait of {trigger} who is a {gender} startup founder in a co-working space or minimalist office, tech-savvy outfit, confident energy, natural lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                column: "PromptTemplate",
                value: "Casual-smart headshot of {trigger} who is a {gender} in a t-shirt and blazer, clean tech-style background, bright lighting, relaxed smile, startup founder vibe");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                column: "PromptTemplate",
                value: "Portrait of {trigger} who is a {gender} tech professional in modern outfit, with a laptop or code in background, neutral tones, focused expression, digital workspace");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                column: "PromptTemplate",
                value: "Trendy portrait of {trigger} who is a {gender} social media influencer with engaging eye contact, soft lighting, fashionable outfit, blurred background, Instagram vibe");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                column: "PromptTemplate",
                value: "Outdoor lifestyle portrait of {trigger} who is a {gender} remote worker, natural lighting, beach or mountain cafe background, laptop in view, relaxed expression");

            // Creative, Lifestyle & Expressive Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                column: "PromptTemplate",
                value: "Colorful and dynamic portrait of {trigger} who is a {gender} artist or creative, expressive pose, vibrant lighting, creative studio background, bold composition");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                column: "PromptTemplate",
                value: "Natural lifestyle photo of {trigger} who is a {gender} in everyday clothing, warm lighting, soft expression, home or park background, candid feel");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                column: "PromptTemplate",
                value: "Fine art portrait of {trigger} who is a {gender} in dramatic lighting, stylized clothing, moody background, painterly composition, thoughtful gaze");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                column: "PromptTemplate",
                value: "Street-style portrait of {trigger} who is a {gender}, gritty city background, bold outfit, high contrast lighting, strong pose, edgy aesthetic");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                column: "PromptTemplate",
                value: "Fashion-inspired portrait of {trigger} who is a {gender} in glamorous makeup and clothing, studio lighting, soft glow effect, luxury editorial feel");

            // Lifestyle & Identity-Focused Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                column: "PromptTemplate",
                value: "Portrait of {trigger} who is a {gender} scholar with books or chalkboard in background, glasses, thoughtful expression, classic academic setting and lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                column: "PromptTemplate",
                value: "Athletic portrait of {trigger} who is a {gender} in workout gear, gym or outdoor fitness location, strong pose, energetic expression, high contrast lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                column: "PromptTemplate",
                value: "Serene portrait of {trigger} who is a {gender} in natural light, peaceful outdoor or temple-like setting, soft expression, spiritual elements like beads or robes");

            // Update credit package timestamps to current migration time
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9960));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9963));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9964));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 30, 11, 37, 30, 470, DateTimeKind.Utc).AddTicks(9966));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert all style prompt templates back to their original form

            // Professional & Career Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                column: "PromptTemplate",
                value: "Professional studio portrait of a {gender} in formal business attire, clean background, confident expression, corporate office lighting, sharp focus");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                column: "PromptTemplate",
                value: "High-end executive portrait of a {gender}, power pose, elegant business suit, luxury office background, natural light, serious expression, premium look");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                column: "PromptTemplate",
                value: "Portrait of a friendly {gender} consultant in semi-formal smart-casual attire, clean background, approachable expression, modern professional tone");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                column: "PromptTemplate",
                value: "Professional LinkedIn-style headshot of a {gender}, neutral background, confident and warm smile, clean business-casual attire, high clarity");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                column: "PromptTemplate",
                value: "Formal portrait of a {gender} lawyer in courtroom or law office, dark tailored suit, serious expression, soft shadows, bookshelf or columns in background");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                column: "PromptTemplate",
                value: "Portrait of a {gender} healthcare professional in lab coat, stethoscope, hospital or clinic background, calm and trustworthy expression, soft light");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                column: "PromptTemplate",
                value: "Intellectual portrait of a {gender} with bookshelves or writing desk in the background, warm ambient lighting, thoughtful gaze, creative professional styling");

            // Modern Entrepreneur & Tech Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                column: "PromptTemplate",
                value: "Modern portrait of a {gender} startup founder in a co-working space or minimalist office, tech-savvy outfit, confident energy, natural lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                column: "PromptTemplate",
                value: "Casual-smart headshot of a {gender} in a t-shirt and blazer, clean tech-style background, bright lighting, relaxed smile, startup founder vibe");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                column: "PromptTemplate",
                value: "Portrait of a {gender} tech professional in modern outfit, with a laptop or code in background, neutral tones, focused expression, digital workspace");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                column: "PromptTemplate",
                value: "Trendy portrait of a {gender} social media influencer with engaging eye contact, soft lighting, fashionable outfit, blurred background, Instagram vibe");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                column: "PromptTemplate",
                value: "Outdoor lifestyle portrait of a {gender} remote worker, natural lighting, beach or mountain cafe background, laptop in view, relaxed expression");

            // Creative, Lifestyle & Expressive Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                column: "PromptTemplate",
                value: "Colorful and dynamic portrait of a {gender} artist or creative, expressive pose, vibrant lighting, creative studio background, bold composition");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                column: "PromptTemplate",
                value: "Natural lifestyle photo of a {gender} in everyday clothing, warm lighting, soft expression, home or park background, candid feel");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                column: "PromptTemplate",
                value: "Fine art portrait of a {gender} in dramatic lighting, stylized clothing, moody background, painterly composition, thoughtful gaze");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                column: "PromptTemplate",
                value: "Street-style portrait of a {gender}, gritty city background, bold outfit, high contrast lighting, strong pose, edgy aesthetic");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                column: "PromptTemplate",
                value: "Fashion-inspired portrait of a {gender} in glamorous makeup and clothing, studio lighting, soft glow effect, luxury editorial feel");

            // Lifestyle & Identity-Focused Styles
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                column: "PromptTemplate",
                value: "Portrait of a {gender} scholar with books or chalkboard in background, glasses, thoughtful expression, classic academic setting and lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                column: "PromptTemplate",
                value: "Athletic portrait of a {gender} in workout gear, gym or outdoor fitness location, strong pose, energetic expression, high contrast lighting");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                column: "PromptTemplate",
                value: "Serene portrait of a {gender} in natural light, peaceful outdoor or temple-like setting, soft expression, spiritual elements like beads or robes");

            // Revert credit package timestamps
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7296));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 6, 26, 12, 18, 42, 456, DateTimeKind.Utc).AddTicks(7301));
        }
    }
}