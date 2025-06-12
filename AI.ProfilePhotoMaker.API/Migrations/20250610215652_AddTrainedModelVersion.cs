using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainedModelVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainedModelVersion",
                table: "ModelCreationRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2199), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2199) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2201), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2202) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2204), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2204) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2206), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2206) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2208), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2208) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2210), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2210) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2212), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2212) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2214), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2214) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2216), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2216) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2217), new DateTime(2025, 6, 10, 21, 56, 52, 4, DateTimeKind.Utc).AddTicks(2218) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainedModelVersion",
                table: "ModelCreationRequests");

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
    }
}
