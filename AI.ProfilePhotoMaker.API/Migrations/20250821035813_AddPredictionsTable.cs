using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Style",
                table: "ProcessedImages",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ModelCreationRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Style = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4204));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4207));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4209));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4341), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4341) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4344), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4344) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4346), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4347) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4348), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4349) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4350), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4351) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4352), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4353) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4354), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4355) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4357), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4358) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4360), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4360) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4362), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4362) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4364), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4364) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4366), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4366) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4368), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4368) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4370), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4370) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4372), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4372) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4374), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4374) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4376), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4376) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4378), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4379) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4380), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4381) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4382), new DateTime(2025, 8, 21, 3, 58, 12, 520, DateTimeKind.Utc).AddTicks(4383) });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId_CreatedAt_Covering",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "CreatedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Id", "Style", "IsGenerated", "IsOriginalUpload" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId_Flags_CreatedAt",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "IsOriginalUpload", "IsGenerated", "CreatedAt" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId_IsGenerated",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "IsGenerated" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId_IsOriginalUpload",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "IsOriginalUpload" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId_Style_CreatedAt_Desc",
                table: "ProcessedImages",
                columns: new[] { "UserProfileId", "Style", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCreationRequests_UserId_Status_CompletedAt",
                table: "ModelCreationRequests",
                columns: new[] { "UserId", "Status", "CompletedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_CreatedAt_Desc",
                table: "Predictions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_UserProfileId_CreatedAt_Covering",
                table: "ProcessedImages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_UserProfileId_Flags_CreatedAt",
                table: "ProcessedImages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_UserProfileId_IsGenerated",
                table: "ProcessedImages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_UserProfileId_IsOriginalUpload",
                table: "ProcessedImages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedImages_UserProfileId_Style_CreatedAt_Desc",
                table: "ProcessedImages");

            migrationBuilder.DropIndex(
                name: "IX_ModelCreationRequests_UserId_Status_CompletedAt",
                table: "ModelCreationRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Style",
                table: "ProcessedImages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ModelCreationRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(8832));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(8834));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(8837));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9148), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9149) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9151), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9151) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9154), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9155) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9156), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9157) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9158), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9159) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9162), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9162) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9164), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9164) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9166), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9166) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9168), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9168) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9171), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9171) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9173), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9173) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9175), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9175) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9177), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9177) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9179), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9179) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9181), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9182) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9183), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9184) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9185), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9186) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9222), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9223) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9224), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9225) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9227), new DateTime(2025, 8, 8, 17, 9, 31, 983, DateTimeKind.Utc).AddTicks(9228) });
        }
    }
}
