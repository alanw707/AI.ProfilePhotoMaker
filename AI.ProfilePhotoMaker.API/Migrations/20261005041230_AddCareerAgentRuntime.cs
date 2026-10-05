using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerAgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareerAgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Task = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PinnedProfileVersion = table.Column<int>(type: "int", nullable: true),
                    PinnedGoalVersion = table.Column<int>(type: "int", nullable: true),
                    CheckpointOrdinal = table.Column<int>(type: "int", nullable: false),
                    CheckpointJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    QuestionId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    QuestionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Answer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FencingToken = table.Column<long>(type: "bigint", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ModelCalled = table.Column<bool>(type: "bit", nullable: false),
                    CostCents = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerAgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerAgentRuns_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerAllowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reserved = table.Column<int>(type: "int", nullable: false),
                    Used = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerAllowances_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CareerAgentSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareerAgentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerAgentSteps_CareerAgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CareerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentRuns_OwnerId_CreatedAt",
                table: "CareerAgentRuns",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentRuns_OwnerId_IdempotencyKey",
                table: "CareerAgentRuns",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentRuns_Status_LeaseExpiresAt",
                table: "CareerAgentRuns",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentSteps_OperationId",
                table: "CareerAgentSteps",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentSteps_OwnerId",
                table: "CareerAgentSteps",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerAgentSteps_RunId",
                table: "CareerAgentSteps",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_CareerAllowances_OwnerId_PeriodStart",
                table: "CareerAllowances",
                columns: new[] { "OwnerId", "PeriodStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerAgentSteps");

            migrationBuilder.DropTable(
                name: "CareerAllowances");

            migrationBuilder.DropTable(
                name: "CareerAgentRuns");
        }
    }
}
