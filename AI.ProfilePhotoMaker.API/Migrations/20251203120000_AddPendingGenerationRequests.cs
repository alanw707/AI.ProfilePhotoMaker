using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingGenerationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure the digital-native style seed does not violate unique index if it already exists
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Styles WHERE Name = 'digital-native')
                BEGIN
                    INSERT INTO Styles (Name, Description, PromptTemplate, NegativePromptTemplate, IsActive, CreatedAt, UpdatedAt)
                    VALUES (
                        'digital-native',
                        'Modern tech creator portrait',
                        '{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality',
                        'outdated technology, old fashioned, formal business, analog aesthetic, traditional office',
                        1,
                        GETUTCDATE(),
                        GETUTCDATE()
                    );
                END
            ");

            migrationBuilder.CreateTable(
                name: "PendingGenerationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrainingRequestId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StylesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumOutputsPerStyle = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastPredictionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingGenerationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingGeneration_UserId_TrainingId",
                table: "PendingGenerationRequests",
                columns: new[] { "UserId", "TrainingRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingGenerationRequests");
        }
    }
}
