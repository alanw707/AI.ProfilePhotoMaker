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
    public virtual DbSet<Prediction> Predictions { get; set; }
    public virtual DbSet<PendingGenerationRequest> PendingGenerationRequests { get; set; }
    public virtual DbSet<RetentionDeletionWarningLog> RetentionDeletionWarningLogs { get; set; }

    // Subscription management
    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public virtual DbSet<Subscription> Subscriptions { get; set; }
    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public virtual DbSet<FeedbackSubmission> FeedbackSubmissions { get; set; }

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
        ConfigureFeedbackSubmissionRelationships(builder);
        ConfigurePendingGenerationRelationships(builder);
        ConfigureCreditPackageRelationships(builder);
        ConfigureRetentionDeletionWarningLogRelationships(builder);

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

    private void ConfigureFeedbackSubmissionRelationships(ModelBuilder builder)
    {
        builder.Entity<FeedbackSubmission>()
            .HasOne(fs => fs.User)
            .WithMany()
            .HasForeignKey(fs => fs.UserId);

        builder.Entity<FeedbackSubmission>()
            .HasIndex(fs => fs.UserId)
            .HasDatabaseName("IX_FeedbackSubmissions_UserId");
    }

    private void ConfigurePendingGenerationRelationships(ModelBuilder builder)
    {
        builder.Entity<PendingGenerationRequest>()
            .HasIndex(p => new { p.UserId, p.TrainingRequestId })
            .HasDatabaseName("IX_PendingGeneration_UserId_TrainingId")
            .IsUnique();
    }

    private void ConfigureRetentionDeletionWarningLogRelationships(ModelBuilder builder)
    {
        builder.Entity<RetentionDeletionWarningLog>()
            .HasIndex(log => new { log.UserId, log.DaysBeforeDeletion, log.DeletionDate })
            .HasDatabaseName("IX_RetentionDeletionWarningLogs_UserId_DaysBeforeDeletion_DeletionDate")
            .IsUnique();
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

        builder.Entity<CreditPurchase>()
            .HasIndex(p => p.PaymentTransactionId)
            .HasFilter("[PaymentTransactionId] IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("IX_CreditPurchases_PaymentTransactionId_Unique");
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

        // Prediction ownership indexes
        builder.Entity<Prediction>()
            .HasIndex(p => p.UserId)
            .HasDatabaseName("IX_Predictions_UserId");
        builder.Entity<Prediction>()
            .HasIndex(p => new { p.UserId, p.CreatedAt })
            .HasDatabaseName("IX_Predictions_UserId_CreatedAt_Desc")
            .IsDescending(false, true);
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
            new Style { Id = 1, Name = "corporate", Description = "Professional corporate headshot style", PromptTemplate = "{subject}, professional corporate headshot of {gender} {ethnicity}, business attire, clean background, confident expression, high-quality photography", NegativePromptTemplate = "casual clothes, blurred, low quality, unprofessional, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 2, Name = "executive", Description = "Executive leadership portrait", PromptTemplate = "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 3, Name = "consultant", Description = "Professional consultant style", PromptTemplate = "{subject}, professional consultant portrait of {gender} {ethnicity}, business consulting style, smart casual attire, approachable yet professional", NegativePromptTemplate = "too casual, unprofessional, blurred, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 4, Name = "linkedin", Description = "LinkedIn professional networking", PromptTemplate = "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 5, Name = "retro-wave", Description = "Modern 80s synthwave aesthetic portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, modern retro wave aesthetic, neon city night backdrop with soft pink and blue accents, soft diffused key light with balanced fill, natural skin tones, realistic skin texture, subtle rim light, shallow depth of field, neon bokeh background, 85mm lens, cinematic color grading, confident trendy expression, contemporary style with vintage flair, subtle film grain", NegativePromptTemplate = "harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 6, Name = "medical", Description = "Healthcare professional style", PromptTemplate = "{subject}, medical professional portrait of {gender} {ethnicity}, healthcare style, professional medical attire, trustworthy healthcare provider appearance, warm caring expression, clinical background", NegativePromptTemplate = "unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 7, Name = "night-out", Description = "Sophisticated evening portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, sophisticated night-out aesthetic, evening city lounge or street bokeh background, ambient street lighting only, soft side key light with gentle fill, no frontal key, no flash, natural skin tones, realistic skin texture, subtle rim light, cinematic color grading, confident social expression, subtle glamour, shallow depth of field, 85mm lens, subtle film grain", NegativePromptTemplate = "harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 8, Name = "entrepreneur", Description = "Entrepreneurial business style", PromptTemplate = "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 9, Name = "startup", Description = "Startup professional style", PromptTemplate = "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 10, Name = "tech-professional", Description = "Technology professional style", PromptTemplate = "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 11, Name = "influencer", Description = "Social media influencer style", PromptTemplate = "{subject}, influencer portrait of {gender} {ethnicity}, engaging personality style, trendy professional appearance, charismatic expression", NegativePromptTemplate = "formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 12, Name = "digital-nomad", Description = "Digital nomad professional", PromptTemplate = "{subject}, digital nomad portrait of {gender} {ethnicity}, remote work professional style, casual modern attire, location-independent professional", NegativePromptTemplate = "formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 13, Name = "creative", Description = "Creative professional style", PromptTemplate = "{subject}, creative professional portrait of {gender} {ethnicity}, modern creative director vibe, stylish contemporary outfit, subtle colorful studio or gallery background, bright airy daylight, playful confident expression, clean editorial photography, head-and-shoulders framing, natural skin texture, minimal retouching, sharp focus, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 14, Name = "casual", Description = "Casual professional style", PromptTemplate = "{subject}, casual lifestyle portrait of {gender} {ethnicity}, everyday clothing (t-shirt/henley/hoodie or sweater, no blazer), relaxed candid smile or laugh, outdoors (park/city street) or cozy home background, golden-hour natural light, relaxed posture (hands in pockets or open gesture), natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 15, Name = "artistic", Description = "Artistic creative portrait", PromptTemplate = "{subject}, fine-art portrait photography of {gender} {ethnicity}, cinematic moody studio lighting, chiaroscuro, textured backdrop, cinematic color grading, contemplative expression, artistic composition, subtle film grain, head-and-shoulders framing, natural skin texture, minimal retouching, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 16, Name = "edgy-urban", Description = "Edgy urban style", PromptTemplate = "{subject}, edgy urban portrait of {gender} {ethnicity}, modern urban style, contemporary city fashion, bold confident expression", NegativePromptTemplate = "conservative formal, traditional business, bland conventional", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 17, Name = "glamour", Description = "Glamour portrait style", PromptTemplate = "{subject}, glamour portrait of {gender} {ethnicity}, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", NegativePromptTemplate = "casual simple, plain appearance, understated look", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 18, Name = "academic", Description = "Academic professional style", PromptTemplate = "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 19, Name = "fitness", Description = "Fitness professional style", PromptTemplate = "{subject}, fitness professional portrait of {gender} {ethnicity}, athletic style, health and wellness appearance, energetic confident look", NegativePromptTemplate = "sedentary look, unhealthy appearance, low energy", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 21, Name = "digital-native", Description = "Modern tech creator portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality", NegativePromptTemplate = "outdated technology, old fashioned, formal business, analog aesthetic, traditional office", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        builder.Entity<Style>().HasData(styles);
    }
}
