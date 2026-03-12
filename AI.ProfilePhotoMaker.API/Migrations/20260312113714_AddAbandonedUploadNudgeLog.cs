using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedUploadNudgeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbandonedUploadNudgeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedUploadNudgeLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7840));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7843));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7845));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7988), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7988) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7990), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7991) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7993), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7993) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7995), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7995) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7997), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(7998) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8000), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8000) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8002), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8002) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8004), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8005) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8007), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8007) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8009), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8009) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8011), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8012) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8013), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8014) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8016), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8016) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8018), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8018) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8020), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8022), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8025), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8027), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8028) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8030), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8030) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8032), new DateTime(2026, 3, 12, 11, 37, 13, 709, DateTimeKind.Utc).AddTicks(8032) });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ResolvedStyleId",
                table: "Predictions",
                column: "ResolvedStyleId");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedUploadNudgeLogs_UserId",
                table: "AbandonedUploadNudgeLogs",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_Styles_ResolvedStyleId",
                table: "Predictions",
                column: "ResolvedStyleId",
                principalTable: "Styles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_Styles_ResolvedStyleId",
                table: "Predictions");

            migrationBuilder.DropTable(
                name: "AbandonedUploadNudgeLogs");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_ResolvedStyleId",
                table: "Predictions");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1130));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1133));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1135));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1275), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1277) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1280), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1280) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1282), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1282) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1284), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1284) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1286), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1287) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1291), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1291) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1294), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1294) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1296), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1296) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1298), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1298) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1300), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1300) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1302), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1302) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1305), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1305) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1307), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1307) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1309), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1309) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1312), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1312) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1314), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1314) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1317), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1317) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1319), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1319) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1321), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1321) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1323), new DateTime(2026, 2, 20, 19, 50, 0, 679, DateTimeKind.Utc).AddTicks(1323) });
        }
    }
}
