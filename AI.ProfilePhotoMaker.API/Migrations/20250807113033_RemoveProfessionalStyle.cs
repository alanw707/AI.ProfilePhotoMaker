using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProfessionalStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete the "professional" style (ID 20) as it has no matching preview image
            migrationBuilder.DeleteData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20);

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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the "professional" style if rolling back
            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[] { 20, new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4330), "Classic professional headshot for general business use", true, "professional", "casual clothes, blurred, low quality, unprofessional", "professional headshot, business attire, clean background, confident expression, high-quality photography", new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4331) });

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

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4278), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4278) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4281), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4282) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4284), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4285) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4287), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4288) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4290), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4290) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4292), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4293) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4295), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4295) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4297), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4298) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4300), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4301) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4303), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4303) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4305), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4305) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4308), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4308) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4310), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4311) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4312), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4313) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4315), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4315) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4317), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4318) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4320), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4320) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4323), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4324) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4327), new DateTime(2025, 8, 6, 20, 6, 45, 352, DateTimeKind.Utc).AddTicks(4328) });

        }
    }
}
