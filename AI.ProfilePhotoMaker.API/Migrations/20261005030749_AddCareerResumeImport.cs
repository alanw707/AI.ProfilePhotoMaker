using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerResumeImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProposalId",
                table: "CareerProfileVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CareerProfileProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResumeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseProfileVersion = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedIntoVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerProfileProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerProfileProposals_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerResumeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsentedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerResumeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerResumeDocuments_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerProfileProposalItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Page = table.Column<int>(type: "int", nullable: true),
                    Section = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Excerpt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Flags = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerProfileProposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerProfileProposalItems_CareerProfileProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "CareerProfileProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileVersions_SourceProposalId",
                table: "CareerProfileVersions",
                column: "SourceProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileProposalItems_OwnerId",
                table: "CareerProfileProposalItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileProposalItems_ProposalId",
                table: "CareerProfileProposalItems",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfileProposals_OwnerId",
                table: "CareerProfileProposals",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerResumeDocuments_ExpiresAt",
                table: "CareerResumeDocuments",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CareerResumeDocuments_OwnerId",
                table: "CareerResumeDocuments",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerProfileProposalItems");

            migrationBuilder.DropTable(
                name: "CareerResumeDocuments");

            migrationBuilder.DropTable(
                name: "CareerProfileProposals");

            migrationBuilder.DropIndex(
                name: "IX_CareerProfileVersions_SourceProposalId",
                table: "CareerProfileVersions");

            migrationBuilder.DropColumn(
                name: "SourceProposalId",
                table: "CareerProfileVersions");
        }
    }
}
