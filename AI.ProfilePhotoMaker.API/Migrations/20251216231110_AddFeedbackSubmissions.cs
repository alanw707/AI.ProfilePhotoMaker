using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LastPredictionId",
                table: "PendingGenerationRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "FeedbackSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackSubmissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2025));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2028));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2030));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2192), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2193) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2196), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2197) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2199), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2199) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2201), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2202) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2204), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2204) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2206), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2207) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2209), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2209) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2211), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2211) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2213), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2214) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2216), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2216) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2218), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2218) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2220), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2221) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2222), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2223) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2225), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2225) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2227), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2227) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2229), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2230) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2232), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2232) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2234), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2234) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2236), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2237) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2239), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2239) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2241), new DateTime(2025, 12, 16, 23, 11, 10, 359, DateTimeKind.Utc).AddTicks(2241) });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSubmissions_UserId",
                table: "FeedbackSubmissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackSubmissions");

            migrationBuilder.AlterColumn<string>(
                name: "LastPredictionId",
                table: "PendingGenerationRequests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(449));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(454));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(458));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(702), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(703) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(811), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(812) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(815), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(816) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(819), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(819) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(822), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(822) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(825), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(825) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(828), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(828) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(832), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(833) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(835), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(836) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(838), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(838) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(842), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(843) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(847), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(848) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(853), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(854) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(857), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(858) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(862), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(863) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(866), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(866) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(871), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(871) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(874), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(874) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(877), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(877) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(881), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(881) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(884), new DateTime(2025, 9, 26, 12, 3, 55, 421, DateTimeKind.Utc).AddTicks(884) });
        }
    }
}
