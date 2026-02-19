using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixSkinBlemishAndWaxyForehead : Migration
    {
        // Skin realism segment as of SoftenSkinRealismConstraints (the @old to replace)
        private const string OldSkinSegment =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity";

        // Strengthened segment: adds forehead-specific waxy terms + blemish terms
        private const string NewSkinSegment =
            "waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, oily forehead, shiny forehead, specular highlights on skin, skin gleam, HDR, oversharpened, too much clarity, moles, dark spots, skin blemishes, birthmarks, skin spots";

        // Terms to append to retro-wave / night-out (which have bespoke templates not matching OldSkinSegment)
        private const string BespokeStyleAppendTerms =
            ", oily forehead, shiny forehead, specular highlights on skin, skin gleam, moles, dark spots, skin blemishes, birthmarks, skin spots";

        private static string Esc(string s) => s.Replace("'", "''");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
-- Fix skin quality: add forehead waxy terms + blemish terms to professional cluster styles
-- These styles end with the exact OldSkinSegment (set by SoftenSkinRealismConstraints)
DECLARE @old nvarchar(max) = '{Esc(OldSkinSegment)}';
DECLARE @new nvarchar(max) = '{Esc(NewSkinSegment)}';

UPDATE dbo.Styles
SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, @old, @new),
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1
  AND NegativePromptTemplate LIKE '%waxy skin%'
  AND LOWER(Name) NOT IN ('retro-wave', 'night-out');

-- Sanity check: if 0 rows updated, the @old string did not match. Fail loudly.
IF @@ROWCOUNT = 0
    RAISERROR('FixSkinBlemishAndWaxyForehead Up(): No rows updated — @old string does not match any active style. Verify against SoftenSkinRealismConstraints.cs.', 16, 1);

-- retro-wave and night-out have bespoke templates that don''t contain OldSkinSegment verbatim.
-- Append missing forehead and blemish terms directly (idempotent: guard on oily forehead).
DECLARE @append nvarchar(max) = '{Esc(BespokeStyleAppendTerms)}';

UPDATE dbo.Styles
SET NegativePromptTemplate = NegativePromptTemplate + @append,
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) IN ('retro-wave', 'night-out')
  AND IsActive = 1
  AND NegativePromptTemplate NOT LIKE '%oily forehead%';
");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8796));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8800));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(8801));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9002), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9004) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9007), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9007) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9009), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9009) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9011), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9011) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9013), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9014) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9015), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9016) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9018), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9019) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9020), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9023), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9025), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9027), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9027) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9030), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9030) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9032), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9032) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9034), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9034) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9036), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9036) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9038), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9038) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9041), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9041) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9043), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9043) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9045), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9045) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9047), new DateTime(2026, 2, 18, 23, 53, 52, 209, DateTimeKind.Utc).AddTicks(9047) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
-- Revert professional cluster styles: swap new segment back to old
DECLARE @new nvarchar(max) = '{Esc(NewSkinSegment)}';
DECLARE @old nvarchar(max) = '{Esc(OldSkinSegment)}';

UPDATE dbo.Styles
SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, @new, @old),
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1
  AND NegativePromptTemplate LIKE '%oily forehead%'
  AND LOWER(Name) NOT IN ('retro-wave', 'night-out');

-- Revert retro-wave / night-out: remove the appended bespoke terms
DECLARE @append nvarchar(max) = '{Esc(BespokeStyleAppendTerms)}';

UPDATE dbo.Styles
SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, @append, ''),
    UpdatedAt = GETUTCDATE()
WHERE LOWER(Name) IN ('retro-wave', 'night-out')
  AND IsActive = 1
  AND NegativePromptTemplate LIKE '%oily forehead%';
");

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8828));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8832));

            migrationBuilder.UpdateData(
                table: "CreditPackages",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8834));

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8991), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8992) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8995), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8995) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8997), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8997) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8999), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(8999) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9001), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9001) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9003), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9003) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9006), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9006) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9008), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9008) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9010), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9011) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9012), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9012) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9014), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9014) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9017), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9017) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9019), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9019) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9021), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9021) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9023), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9023) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9025), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9025) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9028), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9028) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9030), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9030) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9032), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9032) });

            migrationBuilder.UpdateData(
                table: "Styles",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9034), new DateTime(2026, 2, 16, 13, 10, 25, 387, DateTimeKind.Utc).AddTicks(9034) });
        }
    }
}
