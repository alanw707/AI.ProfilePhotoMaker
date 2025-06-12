using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCreationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelCreationRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    ReplicateModelId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingImageZipUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    PendingTrainingRequestId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCreationRequests", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(817), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(818) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(820), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(821) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(823), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(823) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(825), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(825) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(827), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(827) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(829), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(829) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(831), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(831) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(833), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(833) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(835), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(835) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(837), new DateTime(2025, 6, 10, 16, 37, 6, 336, DateTimeKind.Utc).AddTicks(837) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelCreationRequests");

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7705), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7705) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7708), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7708) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7710), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7710) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7712), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7712) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7714), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7714) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7716), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7716) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7718), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7718) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7720), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(7720) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8848), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8849) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8851), new DateTime(2025, 6, 10, 11, 38, 41, 286, DateTimeKind.Utc).AddTicks(8851) });
        }
    }
}
