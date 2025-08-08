using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.ProfilePhotoMaker.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusCredits = table.Column<int>(type: "INTEGER", nullable: false),
                    StripeProductId = table.Column<string>(type: "TEXT", nullable: true),
                    StripePriceId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelCreationRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    ReplicateModelId = table.Column<string>(type: "TEXT", nullable: true),
                    TrainedModelVersion = table.Column<string>(type: "TEXT", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "Styles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PromptTemplate = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    NegativePromptTemplate = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Styles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BillingPeriod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImagesPerMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    CanTrainCustomModels = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanBatchGenerate = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighResolutionOutput = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxTrainingImages = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStylesAccess = table.Column<int>(type: "INTEGER", nullable: false),
                    StripeProductId = table.Column<string>(type: "TEXT", nullable: true),
                    StripePriceId = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreditsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentTransactionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: true),
                    Gender = table.Column<string>(type: "TEXT", nullable: true),
                    Ethnicity = table.Column<string>(type: "TEXT", nullable: true),
                    LastModelSyncCheck = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubscriptionTier = table.Column<int>(type: "INTEGER", nullable: false),
                    Credits = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasedCredits = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCreditReset = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StyleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.UniqueConstraint("AK_UserProfiles_UserId", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Styles_StyleId",
                        column: x => x.StyleId,
                        principalTable: "Styles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PlanId = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalSubscriptionId = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelAtPeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Style = table.Column<string>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOriginalUpload = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScheduledDeletionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedImages_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreditsCost = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditsRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsageLogs_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserStyleSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StyleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStyleSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStyleSelections_Styles_StyleId",
                        column: x => x.StyleId,
                        principalTable: "Styles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserStyleSelections_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "CreditPackages",
                columns: new[] { "Id", "BonusCredits", "CreatedAt", "Credits", "Description", "DisplayOrder", "IsActive", "Name", "Price", "StripePriceId", "StripeProductId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6373), 50, "Perfect for trying out custom training and styled generations", 1, true, "Starter Pack", 9.99m, null, null, null },
                    { 2, 30, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6376), 120, "Most popular - great for professionals", 2, true, "Professional Pack", 19.99m, null, null, null },
                    { 3, 100, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6379), 300, "Best value for content creators and businesses", 3, true, "Studio Pack", 39.99m, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Styles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NegativePromptTemplate", "PromptTemplate", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6559), "Professional corporate headshot style", true, "corporate", "casual clothes, blurred, low quality, unprofessional", "professional corporate headshot, business attire, clean background, confident expression, high-quality photography", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6559) },
                    { 2, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6562), "Executive leadership portrait", true, "executive", "casual, informal, poor lighting, unprofessional", "executive portrait, professional leadership style, formal business attire, authoritative presence, studio lighting", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6562) },
                    { 3, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6564), "Professional consultant style", true, "consultant", "too casual, unprofessional, blurred", "professional consultant portrait, business consulting style, smart casual attire, approachable yet professional", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6564) },
                    { 4, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6566), "LinkedIn professional networking", true, "linkedin", "casual clothes, distracting background, unprofessional", "linkedin profile photo, professional networking style, business attire, friendly professional expression, clean background", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6567) },
                    { 5, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6569), "Legal professional portrait", true, "legal", "casual, informal, unprofessional", "legal professional portrait, formal business attire, trustworthy appearance, professional law office style", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6570) },
                    { 6, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6572), "Healthcare professional style", true, "medical", "casual clothes, unprofessional, poor quality", "medical professional portrait, healthcare style, professional medical attire, trustworthy healthcare provider appearance", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6573) },
                    { 7, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6575), "Author and writer portrait", true, "author", "unprofessional, distracting elements, poor composition", "author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6575) },
                    { 8, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6577), "Entrepreneurial business style", true, "entrepreneur", "formal corporate look, traditional, static pose", "entrepreneur portrait, innovative business leader style, modern professional attire, dynamic confident expression", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6577) },
                    { 9, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6579), "Startup professional style", true, "startup", "overly formal, traditional corporate, stiff pose", "startup professional portrait, innovative tech style, modern casual business attire, entrepreneurial spirit", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6580) },
                    { 10, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6582), "Technology professional style", true, "tech-professional", "overly formal, outdated style, unprofessional", "tech professional portrait, modern technology industry style, smart casual tech attire, innovative professional look", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6582) },
                    { 11, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6584), "Social media influencer style", true, "influencer", "overly formal, corporate look, boring expression", "social media influencer portrait, engaging personality style, trendy professional appearance, charismatic expression", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6584) },
                    { 12, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6586), "Digital nomad professional", true, "digital-nomad", "formal office attire, traditional corporate, static background", "digital nomad portrait, remote work professional style, casual modern attire, location-independent professional", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6586) },
                    { 13, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6588), "Creative professional style", true, "creative", "corporate formal, traditional business, boring conventional look", "creative professional portrait, artistic style, expressive creative look, innovative artistic appearance", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6589) },
                    { 14, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6591), "Casual professional style", true, "casual", "overly formal, stiff corporate, too dressy", "casual professional portrait, relaxed business style, smart casual attire, approachable friendly appearance", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6591) },
                    { 15, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6593), "Artistic creative portrait", true, "artistic", "corporate business, formal attire, conventional look", "artistic portrait, creative artistic style, expressive artistic look, bohemian creative appearance", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6593) },
                    { 16, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6595), "Edgy urban style", true, "edgy-urban", "conservative formal, traditional business, bland conventional", "edgy urban portrait, modern urban style, contemporary city fashion, bold confident expression", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6595) },
                    { 17, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6597), "Glamour portrait style", true, "glamour", "casual simple, plain appearance, understated look", "glamour portrait, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6598) },
                    { 18, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6599), "Academic professional style", true, "academic", "casual informal, unprofessional, non-academic", "academic portrait, scholarly professional style, intellectual appearance, educational professional look", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6601) },
                    { 19, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6602), "Fitness professional style", true, "fitness", "sedentary look, unhealthy appearance, low energy", "fitness professional portrait, athletic style, health and wellness appearance, energetic confident look", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6603) },
                    { 20, new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6605), "Spiritual wellness style", true, "spiritual", "materialistic look, stressed appearance, conventional business", "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic", new DateTime(2025, 8, 7, 16, 13, 56, 332, DateTimeKind.Utc).AddTicks(6605) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_IsActive_DisplayOrder",
                table: "CreditPackages",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_Name_Unique",
                table: "CreditPackages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_PackageId",
                table: "CreditPurchases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_PurchaseDate",
                table: "CreditPurchases",
                column: "PurchaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId",
                table: "CreditPurchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCreationRequests_CreatedAt",
                table: "ModelCreationRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCreationRequests_Status",
                table: "ModelCreationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CreatedAt",
                table: "PaymentTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SubscriptionId",
                table: "PaymentTransactions",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_UserId",
                table: "PaymentTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_CreatedAt",
                table: "ProcessedImages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_ProcessedImageUrl_Unique",
                table: "ProcessedImages",
                column: "ProcessedImageUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedImages_UserProfileId",
                table: "ProcessedImages",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Styles_IsActive",
                table: "Styles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Styles_IsActive_Name",
                table: "Styles",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Styles_Name_Unique",
                table: "Styles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DateRange",
                table: "Subscriptions",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLogs_CreatedAt",
                table: "UsageLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLogs_UserId",
                table: "UsageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_StyleId",
                table: "UserProfiles",
                column: "StyleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStyleSelections_StyleId",
                table: "UserStyleSelections",
                column: "StyleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserStyleSelections_UserProfile_Style_Unique",
                table: "UserStyleSelections",
                columns: new[] { "UserProfileId", "StyleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStyleSelections_UserProfileId",
                table: "UserStyleSelections",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CreditPurchases");

            migrationBuilder.DropTable(
                name: "ModelCreationRequests");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "ProcessedImages");

            migrationBuilder.DropTable(
                name: "UsageLogs");

            migrationBuilder.DropTable(
                name: "UserStyleSelections");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Styles");
        }
    }
}
