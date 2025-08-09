using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }
    public virtual DbSet<ProcessedImage> ProcessedImages { get; set; }
    public virtual DbSet<Style> Styles { get; set; }
    public virtual DbSet<UserStyleSelection> UserStyleSelections { get; set; }
    public virtual DbSet<ModelCreationRequest> ModelCreationRequests { get; set; }
    public virtual DbSet<UsageLog> UsageLogs { get; set; }

    // Subscription management
    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public virtual DbSet<Subscription> Subscriptions { get; set; }
    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    // Premium Package management removed - replaced by unified CreditPackage system

    // Credit Package management (new unified system)
    public virtual DbSet<CreditPackage> CreditPackages { get; set; }
    public virtual DbSet<CreditPurchase> CreditPurchases { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure relationships
        ConfigureUserProfileRelationships(builder);
        ConfigureProcessedImageRelationships(builder);
        ConfigureUsageLogRelationships(builder);
        ConfigureStyleRelationships(builder);
        ConfigureUserStyleSelectionRelationships(builder);
        ConfigureSubscriptionRelationships(builder);
        ConfigurePaymentTransactionRelationships(builder);
        ConfigureCreditPackageRelationships(builder);
        
        // Configure indexes for performance - ENHANCED FOR OPTIMIZATION
        ConfigurePerformanceIndexes(builder);
        
        // Configure decimal precision
        ConfigureDecimalPrecision(builder);

        // Seed data
        SeedCreditPackages(builder);
        SeedStyles(builder);
    }

    private void ConfigureUserProfileRelationships(ModelBuilder builder)
    {
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId);

        builder.Entity<UserProfile>()
            .HasMany(p => p.UsageLogs)
            .WithOne()
            .HasForeignKey(l => l.UserId)
            .HasPrincipalKey(p => p.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private void ConfigureProcessedImageRelationships(ModelBuilder builder)
    {
        builder.Entity<ProcessedImage>()
            .HasOne(i => i.UserProfile)
            .WithMany(p => p.ProcessedImages)
            .HasForeignKey(i => i.UserProfileId);

        // Add unique constraint on ProcessedImageUrl to prevent duplicates
        builder.Entity<ProcessedImage>()
            .HasIndex(i => i.ProcessedImageUrl)
            .IsUnique()
            .HasDatabaseName("IX_ProcessedImages_ProcessedImageUrl_Unique");
    }

    private void ConfigureUsageLogRelationships(ModelBuilder builder)
    {
        builder.Entity<UsageLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId);
    }

    private void ConfigureStyleRelationships(ModelBuilder builder)
    {
        builder.Entity<Style>()
            .HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("IX_Styles_Name_Unique");
    }

    private void ConfigureUserStyleSelectionRelationships(ModelBuilder builder)
    {
        builder.Entity<UserStyleSelection>()
            .HasOne(uss => uss.UserProfile)
            .WithMany()
            .HasForeignKey(uss => uss.UserProfileId);

        builder.Entity<UserStyleSelection>()
            .HasOne(uss => uss.Style)
            .WithMany()
            .HasForeignKey(uss => uss.StyleId);

        // Create unique constraint to prevent duplicate style selections per user
        builder.Entity<UserStyleSelection>()
            .HasIndex(uss => new { uss.UserProfileId, uss.StyleId })
            .IsUnique()
            .HasDatabaseName("IX_UserStyleSelections_UserProfile_Style_Unique");
    }

    private void ConfigureSubscriptionRelationships(ModelBuilder builder)
    {
        builder.Entity<Subscription>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);

        builder.Entity<Subscription>()
            .HasOne(s => s.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PlanId);
    }

    private void ConfigurePaymentTransactionRelationships(ModelBuilder builder)
    {
        builder.Entity<PaymentTransaction>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);

        builder.Entity<PaymentTransaction>()
            .HasOne(t => t.Subscription)
            .WithMany()
            .HasForeignKey(t => t.SubscriptionId);
    }

    private void ConfigureCreditPackageRelationships(ModelBuilder builder)
    {
        builder.Entity<CreditPackage>()
            .HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("IX_CreditPackages_Name_Unique");

        // Configure CreditPurchase relationships
        builder.Entity<CreditPurchase>()
            .HasOne(p => p.Package)
            .WithMany(pkg => pkg.Purchases)
            .HasForeignKey(p => p.PackageId);

        builder.Entity<CreditPurchase>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId);
    }

    private void ConfigurePerformanceIndexes(ModelBuilder builder)
    {
        // User lookup indexes
        builder.Entity<UserProfile>()
            .HasIndex(up => up.UserId)
            .HasDatabaseName("IX_UserProfiles_UserId");

        // ENHANCED ProcessedImage performance indexes for optimized queries
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => pi.UserProfileId)
            .HasDatabaseName("IX_ProcessedImages_UserProfileId");

        // CRITICAL: Combined index for pagination queries (UserProfileId + CreatedAt DESC)
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.CreatedAt })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_CreatedAt_Desc")
            .IsDescending(false, true); // Ascending UserProfileId, Descending CreatedAt

        // OPTIMIZED: Index for filtering by image type
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.IsOriginalUpload })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_IsOriginalUpload");

        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.IsGenerated })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_IsGenerated");

        // OPTIMIZED: Index for style filtering with pagination
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.Style, pi.CreatedAt })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_Style_CreatedAt_Desc")
            .IsDescending(false, false, true); // Ascending UserProfileId and Style, Descending CreatedAt

        // OPTIMIZED: Index for statistics queries (grouped operations)
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.IsOriginalUpload, pi.IsGenerated, pi.CreatedAt })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_Flags_CreatedAt")
            .IsDescending(false, false, false, true);

        // OPTIMIZED: Covering index for common projections (reduces key lookups)
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => new { pi.UserProfileId, pi.CreatedAt })
            .HasDatabaseName("IX_ProcessedImages_UserProfileId_CreatedAt_Covering")
            .IncludeProperties(pi => new { pi.Id, pi.Style, pi.IsGenerated, pi.IsOriginalUpload })
            .IsDescending(false, true);

        // Legacy index - keep for compatibility
        builder.Entity<ProcessedImage>()
            .HasIndex(pi => pi.CreatedAt)
            .HasDatabaseName("IX_ProcessedImages_CreatedAt");

        // UsageLog performance indexes
        builder.Entity<UsageLog>()
            .HasIndex(ul => ul.UserId)
            .HasDatabaseName("IX_UsageLogs_UserId");

        builder.Entity<UsageLog>()
            .HasIndex(ul => ul.CreatedAt)
            .HasDatabaseName("IX_UsageLogs_CreatedAt");

        // Style lookup indexes
        builder.Entity<Style>()
            .HasIndex(s => s.IsActive)
            .HasDatabaseName("IX_Styles_IsActive");

        builder.Entity<Style>()
            .HasIndex(s => new { s.IsActive, s.Name })
            .HasDatabaseName("IX_Styles_IsActive_Name");

        // UserStyleSelection performance indexes
        builder.Entity<UserStyleSelection>()
            .HasIndex(uss => uss.UserProfileId)
            .HasDatabaseName("IX_UserStyleSelections_UserProfileId");

        builder.Entity<UserStyleSelection>()
            .HasIndex(uss => uss.StyleId)
            .HasDatabaseName("IX_UserStyleSelections_StyleId");

        // Subscription performance indexes
        builder.Entity<Subscription>()
            .HasIndex(s => s.UserId)
            .HasDatabaseName("IX_Subscriptions_UserId");

        builder.Entity<Subscription>()
            .HasIndex(s => new { s.StartDate, s.EndDate })
            .HasDatabaseName("IX_Subscriptions_DateRange");

        // Payment transaction indexes
        builder.Entity<PaymentTransaction>()
            .HasIndex(pt => pt.UserId)
            .HasDatabaseName("IX_PaymentTransactions_UserId");

        builder.Entity<PaymentTransaction>()
            .HasIndex(pt => pt.CreatedAt)
            .HasDatabaseName("IX_PaymentTransactions_CreatedAt");

        // Credit package and purchase indexes
        builder.Entity<CreditPackage>()
            .HasIndex(cp => new { cp.IsActive, cp.DisplayOrder })
            .HasDatabaseName("IX_CreditPackages_IsActive_DisplayOrder");

        builder.Entity<CreditPurchase>()
            .HasIndex(cp => cp.UserId)
            .HasDatabaseName("IX_CreditPurchases_UserId");

        builder.Entity<CreditPurchase>()
            .HasIndex(cp => cp.PurchaseDate)
            .HasDatabaseName("IX_CreditPurchases_PurchaseDate");

        // ModelCreationRequest indexes for background service performance
        builder.Entity<ModelCreationRequest>()
            .HasIndex(mcr => mcr.Status)
            .HasDatabaseName("IX_ModelCreationRequests_Status");

        builder.Entity<ModelCreationRequest>()
            .HasIndex(mcr => mcr.CreatedAt)
            .HasDatabaseName("IX_ModelCreationRequests_CreatedAt");

        // ENHANCED: Combined index for user model queries
        builder.Entity<ModelCreationRequest>()
            .HasIndex(mcr => new { mcr.UserId, mcr.Status, mcr.CompletedAt })
            .HasDatabaseName("IX_ModelCreationRequests_UserId_Status_CompletedAt")
            .IsDescending(false, false, true); // Descending CompletedAt for latest first
    }

    private void ConfigureDecimalPrecision(ModelBuilder builder)
    {
        // Configure precision for decimal values
        builder.Entity<SubscriptionPlan>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Entity<PaymentTransaction>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.Entity<CreditPackage>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.Entity<CreditPurchase>()
            .Property(p => p.AmountPaid)
            .HasPrecision(10, 2);
    }

    private void SeedCreditPackages(ModelBuilder builder)
    {
        // Seed credit packages (3 packages with Studio Pack)
        builder.Entity<CreditPackage>().HasData(
            new CreditPackage
            {
                Id = 1,
                Name = "Starter Pack",
                Credits = 50,
                Price = 9.99m,
                Description = "Perfect for trying out custom training and styled generations",
                DisplayOrder = 1,
                BonusCredits = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new CreditPackage
            {
                Id = 2,
                Name = "Professional Pack",
                Credits = 120,
                Price = 19.99m,
                Description = "Most popular - great for professionals",
                DisplayOrder = 2,
                BonusCredits = 30, // Bonus credits for value
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new CreditPackage
            {
                Id = 3,
                Name = "Studio Pack",
                Credits = 300,
                Price = 39.99m,
                Description = "Best value for content creators and businesses",
                DisplayOrder = 3,
                BonusCredits = 100, // Great value with bonus credits
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );
    }

    private void SeedStyles(ModelBuilder builder)
    {
        var styles = new[]
        {
            new Style { Id = 1, Name = "corporate", Description = "Professional corporate headshot style", PromptTemplate = "professional corporate headshot, business attire, clean background, confident expression, high-quality photography", NegativePromptTemplate = "casual clothes, blurred, low quality, unprofessional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 2, Name = "executive", Description = "Executive leadership portrait", PromptTemplate = "executive portrait, professional leadership style, formal business attire, authoritative presence, studio lighting", NegativePromptTemplate = "casual, informal, poor lighting, unprofessional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 3, Name = "consultant", Description = "Professional consultant style", PromptTemplate = "professional consultant portrait, business consulting style, smart casual attire, approachable yet professional", NegativePromptTemplate = "too casual, unprofessional, blurred", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 4, Name = "linkedin", Description = "LinkedIn professional networking", PromptTemplate = "linkedin profile photo, professional networking style, business attire, friendly professional expression, clean background", NegativePromptTemplate = "casual clothes, distracting background, unprofessional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 5, Name = "legal", Description = "Legal professional portrait", PromptTemplate = "legal professional portrait, formal business attire, trustworthy appearance, professional law office style", NegativePromptTemplate = "casual, informal, unprofessional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 6, Name = "medical", Description = "Healthcare professional style", PromptTemplate = "medical professional portrait, healthcare style, professional medical attire, trustworthy healthcare provider appearance", NegativePromptTemplate = "casual clothes, unprofessional, poor quality", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 7, Name = "author", Description = "Author and writer portrait", PromptTemplate = "author portrait, writer style, creative professional appearance, literary aesthetic, thoughtful expression", NegativePromptTemplate = "unprofessional, distracting elements, poor composition", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 8, Name = "entrepreneur", Description = "Entrepreneurial business style", PromptTemplate = "entrepreneur portrait, innovative business leader style, modern professional attire, dynamic confident expression", NegativePromptTemplate = "formal corporate look, traditional, static pose", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 9, Name = "startup", Description = "Startup professional style", PromptTemplate = "startup professional portrait, innovative tech style, modern casual business attire, entrepreneurial spirit", NegativePromptTemplate = "overly formal, traditional corporate, stiff pose", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 10, Name = "tech-professional", Description = "Technology professional style", PromptTemplate = "tech professional portrait, modern technology industry style, smart casual tech attire, innovative professional look", NegativePromptTemplate = "overly formal, outdated style, unprofessional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 11, Name = "influencer", Description = "Social media influencer style", PromptTemplate = "social media influencer portrait, engaging personality style, trendy professional appearance, charismatic expression", NegativePromptTemplate = "overly formal, corporate look, boring expression", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 12, Name = "digital-nomad", Description = "Digital nomad professional", PromptTemplate = "digital nomad portrait, remote work professional style, casual modern attire, location-independent professional", NegativePromptTemplate = "formal office attire, traditional corporate, static background", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 13, Name = "creative", Description = "Creative professional style", PromptTemplate = "creative professional portrait, artistic style, expressive creative look, innovative artistic appearance", NegativePromptTemplate = "corporate formal, traditional business, boring conventional look", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 14, Name = "casual", Description = "Casual professional style", PromptTemplate = "casual professional portrait, relaxed business style, smart casual attire, approachable friendly appearance", NegativePromptTemplate = "overly formal, stiff corporate, too dressy", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 15, Name = "artistic", Description = "Artistic creative portrait", PromptTemplate = "artistic portrait, creative artistic style, expressive artistic look, bohemian creative appearance", NegativePromptTemplate = "corporate business, formal attire, conventional look", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 16, Name = "edgy-urban", Description = "Edgy urban style", PromptTemplate = "edgy urban portrait, modern urban style, contemporary city fashion, bold confident expression", NegativePromptTemplate = "conservative formal, traditional business, bland conventional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 17, Name = "glamour", Description = "Glamour portrait style", PromptTemplate = "glamour portrait, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", NegativePromptTemplate = "casual simple, plain appearance, understated look", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 18, Name = "academic", Description = "Academic professional style", PromptTemplate = "academic portrait, scholarly professional style, intellectual appearance, educational professional look", NegativePromptTemplate = "casual informal, unprofessional, non-academic", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 19, Name = "fitness", Description = "Fitness professional style", PromptTemplate = "fitness professional portrait, athletic style, health and wellness appearance, energetic confident look", NegativePromptTemplate = "sedentary look, unhealthy appearance, low energy", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 20, Name = "spiritual", Description = "Spiritual wellness style", PromptTemplate = "spiritual portrait, wellness style, mindful peaceful appearance, holistic health aesthetic", NegativePromptTemplate = "materialistic look, stressed appearance, conventional business", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        builder.Entity<Style>().HasData(styles);
    }
}