using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionDeletionWarningLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetentionDeletionWarningLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DaysBeforeDeletion = table.Column<int>(type: "int", nullable: false),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionDeletionWarningLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6357));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6363));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6369));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6663), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6664) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6670), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6671) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6675), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6676) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6681), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6682) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6686), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6687) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6691), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6692) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6696), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6697) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6871), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6872) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6877), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6878) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6882), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6883) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6887), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6888) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6892), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6893) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6897), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6897) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6901), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6902) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6906), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6907) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6911), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6912) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6916), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6917) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6921), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6921) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6926), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6926) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6930), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6931) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6935), new DateTime(2025, 12, 26, 20, 3, 10, 253, DateTimeKind.Utc).AddTicks(6936) });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionDeletionWarningLogs_UserId_DaysBeforeDeletion_DeletionDate",
                table: "RetentionDeletionWarningLogs",
                columns: new[] { "UserId", "DaysBeforeDeletion", "DeletionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionDeletionWarningLogs");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1593));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1605));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1610));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1967), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1969) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1974), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1975) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1980), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1981) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1985), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1986) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1990), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1991) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(1996), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2012) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2016), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2017) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2021), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2022) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2026), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2027) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2031), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2032) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2036), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2037) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2041), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2041) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2045), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2046) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2050), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2051) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2055), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2056) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2060), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2061) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2065), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2065) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2069), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2070) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2074), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2075) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2079), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2080) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2084), new DateTime(2025, 12, 26, 19, 56, 5, 191, DateTimeKind.Utc).AddTicks(2085) });
        }
    }
}
