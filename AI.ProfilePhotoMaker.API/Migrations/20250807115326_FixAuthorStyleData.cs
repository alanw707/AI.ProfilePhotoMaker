using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixAuthorStyleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete the incorrect "professional" style (ID 20) that was manually added to production
            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20);

            // Update ID 7 to be "author" style (it was incorrectly showing as "academic")
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate" },
                values: new object[] { 
                    "author", 
                    "Author and writer portrait", 
                    "author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression",
                    "unprofessional, distracting elements, poor composition"
                });

            // Add the correct "spiritual" style at ID 20
            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    20, 
                    new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7102), 
                    "Spiritual wellness style", 
                    true, 
                    "spiritual", 
                    "materialistic look, stressed appearance, conventional business", 
                    "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic", 
                    new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7103) 
                });
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(6736));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(6739));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(6741));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7014), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7015) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7017), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7017) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7019), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7019) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7021), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7023), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7025), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7027), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7028) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7030), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7031) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7032), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7033) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7034), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7035) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7036), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7037) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7038), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7039) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7088), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7089) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7091), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7091) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7093), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7093) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7095), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7095) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7097), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7097) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7098), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7099) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7100), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7101) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7102), new DateTime(2025, 8, 7, 11, 53, 25, 853, DateTimeKind.Utc).AddTicks(7103) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the changes - restore "professional" style at ID 20
            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { 
                    20, 
                    new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8169), 
                    "Classic professional headshot for general business use", 
                    true, 
                    "professional", 
                    "casual clothes, blurred, low quality, unprofessional", 
                    "professional headshot, business attire, clean background, confident expression, high-quality photography", 
                    new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8169) 
                });

            // Restore ID 7 back to "academic"
            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Name", "Description", "PromptTemplate", "NegativePromptTemplate" },
                values: new object[] { 
                    "academic", 
                    "Academic professional for university and research settings", 
                    "academic portrait, scholarly professional style, intellectual appearance, educational professional look",
                    "casual informal, unprofessional, non-academic"
                });
            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(7824));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(7828));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(7830));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8130), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8131) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8134), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8134) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8136), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8136) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8138), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8138) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8141), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8141) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8143), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8143) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8145), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8145) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8147), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8148) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8149), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8149) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8151), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8152) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8153), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8154) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8155), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8156) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8157), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8157) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8159), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8159) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8161), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8161) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8163), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8163) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8165), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8165) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8167), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8167) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8169), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8169) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8171), new DateTime(2025, 8, 7, 11, 30, 33, 335, DateTimeKind.Utc).AddTicks(8171) });
        }
    }
}
