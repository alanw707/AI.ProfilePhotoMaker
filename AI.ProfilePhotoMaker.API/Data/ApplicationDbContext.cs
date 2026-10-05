using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Fixed timestamp for seed data so EF migrations don't generate
    /// spurious UPDATE statements every time a migration is scaffolded.
    /// </summary>
    private static readonly DateTime SeedTimestamp =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
    public virtual DbSet<HeadshotGenerationOperation> HeadshotGenerationOperations { get; set; }
    public virtual DbSet<RetentionDeletionWarningLog> RetentionDeletionWarningLogs { get; set; }
    public virtual DbSet<AbandonedUploadNudgeLog> AbandonedUploadNudgeLogs { get; set; }

    // Subscription management
    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public virtual DbSet<Subscription> Subscriptions { get; set; }
    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public virtual DbSet<StripeWebhookOperation> StripeWebhookOperations { get; set; }
    public virtual DbSet<FeedbackSubmission> FeedbackSubmissions { get; set; }

    // Premium Package management removed - replaced by unified CreditPackage system

    // Credit Package management (new unified system)
    public virtual DbSet<CreditPackage> CreditPackages { get; set; }
    public virtual DbSet<CreditPurchase> CreditPurchases { get; set; }
    public virtual DbSet<OutcomePackageDefinition> OutcomePackageDefinitions { get; set; }
    public virtual DbSet<UserPackageEntitlement> UserPackageEntitlements { get; set; }
    public virtual DbSet<AdminAuditLog> AdminAuditLogs { get; set; }
    public virtual DbSet<Coupon> Coupons { get; set; }
    public virtual DbSet<CouponRedemption> CouponRedemptions { get; set; }

    // Marketing
    public virtual DbSet<MarketingCampaign> MarketingCampaigns { get; set; }
    public virtual DbSet<MarketingEmailLog> MarketingEmailLogs { get; set; }

    // Career workspace (spec #376, ADR 0006). Private, owner-scoped data.
    public virtual DbSet<Models.Career.CareerProfile> CareerProfiles { get; set; }
    public virtual DbSet<Models.Career.CareerProfileVersion> CareerProfileVersions { get; set; }
    public virtual DbSet<Models.Career.CareerGoal> CareerGoals { get; set; }
    public virtual DbSet<Models.Career.CareerGoalVersion> CareerGoalVersions { get; set; }
    public virtual DbSet<Models.Career.ResumeDocument> CareerResumeDocuments { get; set; }
    public virtual DbSet<Models.Career.CareerProfileProposal> CareerProfileProposals { get; set; }
    public virtual DbSet<Models.Career.CareerProfileProposalItem> CareerProfileProposalItems { get; set; }
    public virtual DbSet<Models.Career.CareerPhotoSelection> CareerPhotoSelections { get; set; }
    public virtual DbSet<Models.Career.CareerAgentRun> CareerAgentRuns { get; set; }
    public virtual DbSet<Models.Career.CareerAgentStep> CareerAgentSteps { get; set; }
    public virtual DbSet<Models.Career.CareerAllowance> CareerAllowances { get; set; }
    public virtual DbSet<Models.Career.CareerOccupationMatch> CareerOccupationMatches { get; set; }
    public virtual DbSet<Models.Career.CareerMarketBrief> CareerMarketBriefs { get; set; }
    public virtual DbSet<Models.Career.CareerPayAnalysis> CareerPayAnalyses { get; set; }
    public virtual DbSet<Models.Career.CareerRoadmap> CareerRoadmaps { get; set; }
    public virtual DbSet<Models.Career.CareerRoadmapTaskProgress> CareerRoadmapTaskProgress { get; set; }
    public virtual DbSet<Models.Career.CareerRoadmapReplan> CareerRoadmapReplans { get; set; }

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
        ConfigureStripeWebhookOperations(builder);
        ConfigureFeedbackSubmissionRelationships(builder);
        ConfigurePendingGenerationRelationships(builder);
        ConfigureHeadshotGenerationOperations(builder);
        ConfigureCreditPackageRelationships(builder);
        ConfigureOutcomePackageRelationships(builder);
        ConfigureAdminRelationships(builder);
        ConfigureRetentionDeletionWarningLogRelationships(builder);
        ConfigureAbandonedUploadNudgeLogRelationships(builder);
        ConfigurePredictionRelationships(builder);
        ConfigureMarketingRelationships(builder);
        ConfigureCareerWorkspace(builder);

        // Configure indexes for performance - ENHANCED FOR OPTIMIZATION
        ConfigurePerformanceIndexes(builder);

        // Configure decimal precision
        ConfigureDecimalPrecision(builder);

        // Seed data
        SeedCreditPackages(builder);
        SeedOutcomePackageDefinitions(builder);
        SeedStyles(builder);
    }


    private void ConfigureUserProfileRelationships(ModelBuilder builder)
    {
        // Every tracked balance write must match the balance originally read. This
        // protects purchases, generation, refunds and resets across API replicas.
        builder.Entity<UserProfile>().Property(p => p.Credits).IsConcurrencyToken();

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

        builder.Entity<ProcessedImage>()
            .HasIndex(i => new { i.UserProfileId, i.GenerationMode, i.CreatedAt })
            .HasDatabaseName("IX_ProcessedImages_User_Mode_CreatedAt");

        builder.Entity<ProcessedImage>()
            .Property(i => i.Provider)
            .HasMaxLength(64);

        builder.Entity<ProcessedImage>()
            .Property(i => i.ProviderModel)
            .HasMaxLength(128);

        builder.Entity<ProcessedImage>()
            .Property(i => i.GenerationMode)
            .HasMaxLength(64);

        builder.Entity<ProcessedImage>()
            .Property(i => i.PromptVersion)
            .HasMaxLength(128);

        builder.Entity<ProcessedImage>()
            .Property(i => i.GenerationStatus)
            .HasMaxLength(64);

        builder.Entity<ProcessedImage>()
            .Property(i => i.CorrelationId)
            .HasMaxLength(128);

        builder.Entity<ProcessedImage>()
            .Property(i => i.GenerationOperationToken)
            .HasMaxLength(64);
    }

    private static void ConfigureCareerWorkspace(ModelBuilder builder)
    {
        // Owner rows cascade from the Identity user so account removal cannot leave
        // career data behind. Versions cascade from their aggregate. The active
        // version is a number, not a foreign key, to avoid a cycle.
        var profile = builder.Entity<Models.Career.CareerProfile>();
        profile.ToTable("CareerProfiles");
        profile.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();
        profile.HasIndex(p => p.OwnerId).IsUnique();
        profile.Property(p => p.ActiveVersionNumber).IsConcurrencyToken();
        profile.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        profile.HasMany(p => p.Versions).WithOne(v => v.CareerProfile!).HasForeignKey(v => v.CareerProfileId).OnDelete(DeleteBehavior.Cascade);

        var profileVersion = builder.Entity<Models.Career.CareerProfileVersion>();
        profileVersion.ToTable("CareerProfileVersions");
        profileVersion.Property(v => v.OwnerId).HasMaxLength(450).IsRequired();
        profileVersion.HasIndex(v => new { v.CareerProfileId, v.VersionNumber }).IsUnique();
        profileVersion.HasIndex(v => v.OwnerId);
        profileVersion.Property(v => v.CurrentTitle).HasMaxLength(120).IsRequired();
        profileVersion.Property(v => v.Industry).HasMaxLength(120);
        profileVersion.Property(v => v.Location).HasMaxLength(120);
        profileVersion.Property(v => v.Summary).HasMaxLength(2000);
        profileVersion.Property(v => v.WorkArrangement).HasMaxLength(20);
        profileVersion.Property(v => v.Source).HasMaxLength(32).IsRequired();
        profileVersion.HasIndex(v => v.SourceProposalId);

        var goal = builder.Entity<Models.Career.CareerGoal>();
        goal.ToTable("CareerGoals");
        goal.Property(g => g.OwnerId).HasMaxLength(450).IsRequired();
        goal.HasIndex(g => g.OwnerId).IsUnique();
        goal.Property(g => g.ActiveVersionNumber).IsConcurrencyToken();
        goal.HasOne<ApplicationUser>().WithMany().HasForeignKey(g => g.OwnerId).OnDelete(DeleteBehavior.Cascade);
        goal.HasMany(g => g.Versions).WithOne(v => v.CareerGoal!).HasForeignKey(v => v.CareerGoalId).OnDelete(DeleteBehavior.Cascade);

        var goalVersion = builder.Entity<Models.Career.CareerGoalVersion>();
        goalVersion.ToTable("CareerGoalVersions");
        goalVersion.Property(v => v.OwnerId).HasMaxLength(450).IsRequired();
        goalVersion.HasIndex(v => new { v.CareerGoalId, v.VersionNumber }).IsUnique();
        goalVersion.HasIndex(v => v.OwnerId);
        goalVersion.Property(v => v.TargetRole).HasMaxLength(120).IsRequired();
        goalVersion.Property(v => v.TargetLocation).HasMaxLength(120);
        goalVersion.Property(v => v.WorkArrangement).HasMaxLength(20);
        goalVersion.Property(v => v.Source).HasMaxLength(32).IsRequired();
        goalVersion.Property(v => v.OccupationCode).HasMaxLength(10);
        goalVersion.Property(v => v.OccupationTitle).HasMaxLength(200);
        goalVersion.Property(v => v.OccupationReferenceRelease).HasMaxLength(20);
        goalVersion.Property(v => v.PreferredAreaCode).HasMaxLength(10);
        goalVersion.Property(v => v.PreferredAreaTitle).HasMaxLength(200);
        goalVersion.Property(v => v.PreferredAreaLevel).HasMaxLength(10);

        // Resume import (#379, ADR 0007). Documents and proposals reference each other
        // by plain id only, so deleting either never needs a cascade across the pair.
        var resume = builder.Entity<Models.Career.ResumeDocument>();
        resume.ToTable("CareerResumeDocuments");
        resume.Property(d => d.OwnerId).HasMaxLength(450).IsRequired();
        resume.HasIndex(d => d.OwnerId);
        resume.HasIndex(d => d.ExpiresAt);
        resume.Property(d => d.StorageKey).HasMaxLength(200).IsRequired();
        resume.Property(d => d.FileName).HasMaxLength(200).IsRequired();
        resume.Property(d => d.Format).HasMaxLength(10).IsRequired();
        resume.Property(d => d.Sha256).HasMaxLength(64).IsRequired();
        resume.Property(d => d.State).HasConversion<string>().HasMaxLength(20);
        resume.Property(d => d.FailureCode).HasMaxLength(40);
        resume.Property(d => d.ConsentVersion).HasMaxLength(64).IsRequired();
        resume.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Cascade);

        var proposal = builder.Entity<Models.Career.CareerProfileProposal>();
        proposal.ToTable("CareerProfileProposals");
        proposal.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();
        proposal.HasIndex(p => p.OwnerId);
        proposal.Property(p => p.Source).HasMaxLength(16).IsRequired();
        proposal.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        proposal.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
        proposal.HasMany(p => p.Items).WithOne(i => i.Proposal!).HasForeignKey(i => i.ProposalId).OnDelete(DeleteBehavior.Cascade);

        var proposalItem = builder.Entity<Models.Career.CareerProfileProposalItem>();
        proposalItem.ToTable("CareerProfileProposalItems");
        proposalItem.Property(i => i.OwnerId).HasMaxLength(450).IsRequired();
        proposalItem.HasIndex(i => i.OwnerId);
        proposalItem.Property(i => i.Field).HasMaxLength(32).IsRequired();
        proposalItem.Property(i => i.Value).HasMaxLength(2000).IsRequired();
        proposalItem.Property(i => i.Section).HasMaxLength(60);
        proposalItem.Property(i => i.Excerpt).HasMaxLength(300).IsRequired();

        // ProcessedImageId is intentionally not a foreign key (ADR 0008): photo
        // retention must never be blocked by, or cascade into, career data.
        var photoSelection = builder.Entity<Models.Career.CareerPhotoSelection>();
        photoSelection.ToTable("CareerPhotoSelections");
        photoSelection.Property(s => s.OwnerId).HasMaxLength(450).IsRequired();
        photoSelection.HasIndex(s => s.OwnerId).IsUnique();
        photoSelection.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Agent runtime (#380, ADR 0009). Runs, steps and allowances cascade from the user.
        var run = builder.Entity<Models.Career.CareerAgentRun>();
        run.ToTable("CareerAgentRuns");
        run.Property(r => r.OwnerId).HasMaxLength(450).IsRequired();
        run.Property(r => r.Task).HasMaxLength(40).IsRequired();
        run.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        run.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired();
        run.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        run.Property(r => r.CheckpointJson).HasMaxLength(2000);
        run.Property(r => r.QuestionId).HasMaxLength(40);
        run.Property(r => r.QuestionText).HasMaxLength(200);
        run.Property(r => r.Answer).HasMaxLength(200);
        run.Property(r => r.LeaseOwner).HasMaxLength(100);
        run.Property(r => r.ErrorCode).HasMaxLength(40);
        run.Property(r => r.FencingToken).IsConcurrencyToken();
        run.HasIndex(r => new { r.OwnerId, r.IdempotencyKey }).IsUnique();
        run.HasIndex(r => new { r.OwnerId, r.CreatedAt });
        run.HasIndex(r => new { r.Status, r.LeaseExpiresAt });
        run.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
        run.HasMany(r => r.Steps).WithOne(s => s.Run!).HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);

        var step = builder.Entity<Models.Career.CareerAgentStep>();
        step.ToTable("CareerAgentSteps");
        step.Property(s => s.OwnerId).HasMaxLength(450).IsRequired();
        step.Property(s => s.OperationId).HasMaxLength(80).IsRequired();
        step.Property(s => s.Kind).HasMaxLength(16).IsRequired();
        step.Property(s => s.Name).HasMaxLength(40).IsRequired();
        step.Property(s => s.Status).HasMaxLength(16).IsRequired();
        step.HasIndex(s => s.OperationId).IsUnique();
        step.HasIndex(s => s.OwnerId);

        var allowance = builder.Entity<Models.Career.CareerAllowance>();
        allowance.ToTable("CareerAllowances");
        allowance.Property(a => a.OwnerId).HasMaxLength(450).IsRequired();
        allowance.Property(a => a.Version).IsConcurrencyToken();
        allowance.HasIndex(a => new { a.OwnerId, a.PeriodStart }).IsUnique();
        allowance.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Occupation matches (#381, ADR 0010). One per run; cascade from the user.
        var match = builder.Entity<Models.Career.CareerOccupationMatch>();
        match.ToTable("CareerOccupationMatches");
        match.Property(m => m.OwnerId).HasMaxLength(450).IsRequired();
        match.Property(m => m.ReferenceRelease).HasMaxLength(20).IsRequired();
        match.Property(m => m.MatcherVersion).HasMaxLength(40).IsRequired();
        // A match is decided once: a dismiss and a confirm that both read "proposed" cannot both win.
        match.Property(m => m.Status).HasMaxLength(16).IsRequired().IsConcurrencyToken();
        match.Property(m => m.ConfirmedCode).HasMaxLength(10);
        match.HasIndex(m => m.RunId).IsUnique();
        match.HasIndex(m => new { m.OwnerId, m.CreatedAt });
        match.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Market briefs (#382, ADR 0011). One per run; cascade from the user.
        var brief = builder.Entity<Models.Career.CareerMarketBrief>();
        brief.ToTable("CareerMarketBriefs");
        brief.Property(b => b.OwnerId).HasMaxLength(450).IsRequired();
        brief.Property(b => b.OccupationCode).HasMaxLength(10).IsRequired();
        brief.Property(b => b.OccupationTitle).HasMaxLength(200).IsRequired();
        brief.Property(b => b.OewsRelease).HasMaxLength(20);
        brief.Property(b => b.ProjectionsRelease).HasMaxLength(20);
        brief.Property(b => b.Status).HasMaxLength(16).IsRequired();
        brief.HasIndex(b => b.RunId).IsUnique();
        brief.HasIndex(b => new { b.OwnerId, b.CreatedAt });
        brief.HasOne<ApplicationUser>().WithMany().HasForeignKey(b => b.OwnerId).OnDelete(DeleteBehavior.Cascade);

        var pay = builder.Entity<Models.Career.CareerPayAnalysis>();
        pay.ToTable("CareerPayAnalyses");
        pay.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();
        pay.Property(p => p.OccupationCode).HasMaxLength(10).IsRequired();
        pay.Property(p => p.OccupationTitle).HasMaxLength(200).IsRequired();
        pay.Property(p => p.AreaCode).HasMaxLength(20);
        pay.Property(p => p.AreaTitle).HasMaxLength(200);
        pay.Property(p => p.AreaResolution).HasMaxLength(20).IsRequired();
        pay.Property(p => p.LocationInput).HasMaxLength(120);
        pay.Property(p => p.RequestedPaySource).HasMaxLength(20);
        pay.Property(p => p.OewsRelease).HasMaxLength(20);
        pay.Property(p => p.OewsSnapshotSha256).HasMaxLength(64).IsRequired();
        pay.Property(p => p.ProjectionsRelease).HasMaxLength(20);
        pay.Property(p => p.RuleVersion).HasMaxLength(40).IsRequired();
        pay.Property(p => p.ObservationSourceId).HasMaxLength(100);
        pay.Property(p => p.InputHash).HasMaxLength(64).IsRequired();
        pay.Property(p => p.Status).HasMaxLength(16).IsRequired();
        pay.HasIndex(p => p.RunId).IsUnique();
        pay.HasIndex(p => new { p.OwnerId, p.CreatedAt });
        pay.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Roadmaps (#387, ADR 0016). Owner cascade; edits write new versions, so RunId is null on those.
        var roadmap = builder.Entity<Models.Career.CareerRoadmap>();
        roadmap.ToTable("CareerRoadmaps");
        roadmap.Property(r => r.OwnerId).HasMaxLength(450).IsRequired();
        roadmap.Property(r => r.Status).HasMaxLength(16).IsRequired().IsConcurrencyToken();
        roadmap.Property(r => r.SelectedOption).HasMaxLength(32);
        roadmap.Property(r => r.OccupationCode).HasMaxLength(10).IsRequired();
        roadmap.Property(r => r.OccupationTitle).HasMaxLength(200).IsRequired();
        roadmap.Property(r => r.LowTimeNote).HasMaxLength(400);
        roadmap.HasIndex(r => r.RunId).IsUnique();
        roadmap.HasIndex(r => new { r.OwnerId, r.Version });
        roadmap.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Roadmap tracking (#388, ADR 0017). Owner cascade; rows are also removed explicitly by CareerPrivateDataService.
        var progress = builder.Entity<Models.Career.CareerRoadmapTaskProgress>();
        progress.ToTable("CareerRoadmapTaskProgress");
        progress.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();
        progress.Property(p => p.TaskId).HasMaxLength(32).IsRequired();
        progress.Property(p => p.Origin).HasMaxLength(16).IsRequired();
        progress.Property(p => p.Title).HasMaxLength(200);
        progress.Property(p => p.DependsOnJson).HasMaxLength(2000);
        progress.Property(p => p.Status).HasMaxLength(16).IsRequired();
        progress.Property(p => p.OutputNote).HasMaxLength(2000);
        progress.Property(p => p.RowVersion).IsConcurrencyToken();
        progress.HasIndex(p => new { p.RoadmapId, p.TaskId }).IsUnique();
        progress.HasIndex(p => p.OwnerId);
        progress.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);

        var replan = builder.Entity<Models.Career.CareerRoadmapReplan>();
        replan.ToTable("CareerRoadmapReplans");
        replan.Property(r => r.OwnerId).HasMaxLength(450).IsRequired();
        replan.Property(r => r.Status).HasMaxLength(16).IsRequired().IsConcurrencyToken();
        replan.HasIndex(r => new { r.OwnerId, r.CreatedAt });
        replan.HasIndex(r => r.RoadmapId);
        replan.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureHeadshotGenerationOperations(ModelBuilder builder)
    {
        builder.Entity<HeadshotGenerationOperation>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<HeadshotGenerationOperation>()
            .HasIndex(o => o.CorrelationId)
            .IsUnique();

        builder.Entity<HeadshotGenerationOperation>()
            .HasIndex(o => new { o.Status, o.LeaseExpiresAt });
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

    private void ConfigureAbandonedUploadNudgeLogRelationships(ModelBuilder builder)
    {
        // One nudge email per user and abandoned-upload state.
        builder.Entity<AbandonedUploadNudgeLog>()
            .HasIndex(log => new { log.UserId, log.NudgeType })
            .HasDatabaseName("IX_AbandonedUploadNudgeLogs_UserId_NudgeType")
            .IsUnique();
    }

    private void ConfigurePredictionRelationships(ModelBuilder builder)
    {
        builder.Entity<Prediction>()
            .HasIndex(p => p.ResolvedStyleId)
            .HasDatabaseName("IX_Predictions_ResolvedStyleId");

        builder.Entity<Prediction>()
            .HasOne<Style>()
            .WithMany()
            .HasForeignKey(p => p.ResolvedStyleId)
            .OnDelete(DeleteBehavior.SetNull);
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

    private void ConfigureOutcomePackageRelationships(ModelBuilder builder)
    {
        // Conditional writes protect all allowance fields, including terminal status.
        var entitlement = builder.Entity<UserPackageEntitlement>();
        entitlement.Property(e => e.RemainingPackageUses).IsConcurrencyToken();
        entitlement.Property(e => e.RemainingCandidates).IsConcurrencyToken();
        entitlement.Property(e => e.RemainingRefinements).IsConcurrencyToken();
        entitlement.Property(e => e.RemainingPremiumAugmentations).IsConcurrencyToken();
        entitlement.Property(e => e.PlatformExportKitAvailable).IsConcurrencyToken();
        entitlement.Property(e => e.Status).IsConcurrencyToken();
        entitlement.Property(e => e.ExpiresAt).IsConcurrencyToken();

        builder.Entity<OutcomePackageDefinition>()
            .HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("IX_OutcomePackageDefinitions_Code_Unique");

        builder.Entity<OutcomePackageDefinition>()
            .HasOne(p => p.InternalCreditPackage)
            .WithMany()
            .HasForeignKey(p => p.InternalCreditPackageId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<UserPackageEntitlement>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserPackageEntitlement>()
            .HasOne(e => e.OutcomePackageDefinition)
            .WithMany(p => p.Entitlements)
            .HasForeignKey(e => e.OutcomePackageDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserPackageEntitlement>()
            .HasOne(e => e.SourcePaymentTransaction)
            .WithMany()
            .HasForeignKey(e => e.SourcePaymentTransactionId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private void ConfigureStripeWebhookOperations(ModelBuilder builder)
    {
        builder.Entity<StripeWebhookOperation>()
            .HasIndex(o => o.OperationKey)
            .IsUnique();

        builder.Entity<StripeWebhookOperation>()
            .HasIndex(o => o.StripeEventId)
            .IsUnique();

        builder.Entity<StripeWebhookOperation>()
            .HasIndex(o => new { o.Status, o.LeaseExpiresAt });
    }

    private void ConfigureAdminRelationships(ModelBuilder builder)
    {
        // Different payments share coupon capacity; receipt fencing alone cannot
        // prevent a stale redemption from spending the final use twice.
        var coupon = builder.Entity<Coupon>();
        coupon.Property(c => c.CurrentUsages).IsConcurrencyToken();
        coupon.Property(c => c.MaxUsages).IsConcurrencyToken();
        coupon.Property(c => c.IsActive).IsConcurrencyToken();
        coupon.Property(c => c.ExpiresAt).IsConcurrencyToken();

        builder.Entity<AdminAuditLog>()
            .HasIndex(l => new { l.AdminUserId, l.CreatedAt })
            .HasDatabaseName("IX_AdminAuditLogs_AdminUserId_CreatedAt");

        builder.Entity<Coupon>()
            .HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Coupons_Code_Unique");

        builder.Entity<Coupon>()
            .HasMany(c => c.Redemptions)
            .WithOne(r => r.Coupon)
            .HasForeignKey(r => r.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CouponRedemption>()
            .HasIndex(r => new { r.CouponId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_CouponRedemptions_CouponId_UserId_Unique");

        builder.Entity<CouponRedemption>()
            .HasIndex(r => r.PaymentTransactionId)
            .IsUnique();

        builder.Entity<CouponRedemption>()
            .HasOne<PaymentTransaction>()
            .WithMany()
            .HasForeignKey(r => r.PaymentTransactionId)
            .OnDelete(DeleteBehavior.NoAction);
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

        builder.Entity<OutcomePackageDefinition>()
            .HasIndex(op => new { op.IsActive, op.DisplayOrder })
            .HasDatabaseName("IX_OutcomePackageDefinitions_IsActive_DisplayOrder");

        builder.Entity<UserPackageEntitlement>()
            .HasIndex(e => new { e.UserId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_UserPackageEntitlements_User_Status_CreatedAt");

        builder.Entity<UserPackageEntitlement>()
            .HasIndex(e => e.SourcePaymentTransactionId)
            .HasFilter("[SourcePaymentTransactionId] IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("IX_UserPackageEntitlements_SourcePaymentTransactionId_Unique");

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

        builder.Entity<OutcomePackageDefinition>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.Entity<Coupon>()
            .Property(c => c.DiscountValue)
            .HasPrecision(10, 2);

        builder.Entity<CouponRedemption>()
            .Property(r => r.DiscountApplied)
            .HasPrecision(10, 2);

        builder.Entity<CouponRedemption>()
            .Property(r => r.OriginalPrice)
            .HasPrecision(10, 2);

        builder.Entity<CouponRedemption>()
            .Property(r => r.FinalPrice)
            .HasPrecision(10, 2);
    }

    private void ConfigureMarketingRelationships(ModelBuilder builder)
    {
        builder.Entity<MarketingCampaign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(300).IsRequired();
            entity.Property(e => e.SegmentFilter).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_MarketingCampaigns_CreatedAt");
        });

        builder.Entity<MarketingEmailLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PostmarkMessageId).HasMaxLength(128);
            entity.HasOne(e => e.Campaign)
                  .WithMany()
                  .HasForeignKey(e => e.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CampaignId).HasDatabaseName("IX_MarketingEmailLogs_CampaignId");
            entity.HasIndex(e => e.PostmarkMessageId).HasDatabaseName("IX_MarketingEmailLogs_PostmarkMessageId");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_MarketingEmailLogs_UserId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_MarketingEmailLogs_Status");
            // Unique constraint: one log entry per user per campaign
            entity.HasIndex(e => new { e.CampaignId, e.UserId })
                  .IsUnique()
                  .HasDatabaseName("UX_MarketingEmailLogs_CampaignId_UserId");
        });
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
                CreatedAt = SeedTimestamp
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
                CreatedAt = SeedTimestamp
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
                CreatedAt = SeedTimestamp
            }
        );
    }

    private void SeedOutcomePackageDefinitions(ModelBuilder builder)
    {
        builder.Entity<OutcomePackageDefinition>().HasData(
            new OutcomePackageDefinition
            {
                Id = 1,
                Code = "free_preview",
                Name = "Free Preview",
                Description = "Score your source photo and try a same-quality watermarked preview before buying a package.",
                Price = 0m,
                Currency = "USD",
                InternalCreditPackageId = null,
                IncludedCandidateCount = 1,
                IncludedRefinementCount = 0,
                IncludedPremiumAugmentationCount = 0,
                IncludesPlatformExportKit = false,
                IncludesScoreDelta = false,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAt = SeedTimestamp
            },
            new OutcomePackageDefinition
            {
                Id = 2,
                Code = "starter_package",
                Name = "Starter Package",
                Description = "Three profile-photo candidates, best shot selector, basic adjustment, and selected platform exports.",
                Price = 9.99m,
                Currency = "USD",
                InternalCreditPackageId = 1,
                IncludedCandidateCount = 3,
                IncludedRefinementCount = 2,
                IncludedPremiumAugmentationCount = 0,
                IncludesPlatformExportKit = true,
                IncludesScoreDelta = false,
                IsActive = true,
                DisplayOrder = 2,
                CreatedAt = SeedTimestamp
            },
            new OutcomePackageDefinition
            {
                Id = 3,
                Code = "pro_package",
                Name = "Pro Package",
                Description = "Nine candidates, best shot selector, score delta, exports, refinements, and premium augmentations.",
                Price = 19.99m,
                Currency = "USD",
                InternalCreditPackageId = 2,
                IncludedCandidateCount = 9,
                IncludedRefinementCount = 5,
                IncludedPremiumAugmentationCount = 3,
                IncludesPlatformExportKit = true,
                IncludesScoreDelta = true,
                IsActive = true,
                DisplayOrder = 3,
                CreatedAt = SeedTimestamp
            }
        );
    }

    private void SeedStyles(ModelBuilder builder)
    {
        var styles = new[]
        {
            // Id 1: beach-vibes (was "corporate" — fixed in FixStylePromptsDataDriftAndQualityAudit migration)
            new Style { Id = 1, Name = "beach-vibes", Description = "Sun-kissed vacation mode portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, subtle beach vacation aesthetic, sun-kissed healthy glow, soft coastal background with blurred ocean hints, warm golden hour lighting, relaxed confident expression, casual summer style, natural beachy hair texture, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, winter clothes, cold weather, indoor office, formal business attire, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 2, Name = "executive", Description = "Executive leadership portrait", PromptTemplate = "{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            // Id 3: fresh (was "consultant" — fixed in FixStylePromptsDataDriftAndQualityAudit migration)
            new Style { Id = 3, Name = "fresh", Description = "Clean energetic refreshed portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, fresh clean aesthetic, dewy glowing skin, bright airy background, soft natural lighting, energetic refreshed expression, healthy vitality, natural skin texture, minimal retouching, head-and-shoulders framing", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, tired, exhausted, dull skin, dark shadows, heavy makeup, messy appearance, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 4, Name = "linkedin", Description = "LinkedIn professional networking", PromptTemplate = "{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), clean professional background with subtle variety such as a soft neutral gradient (warm gray, ivory, muted taupe, or soft slate), a clean off-white or warm gray studio backdrop, or a minimal modern office interior with gentle bokeh, professional and uncluttered, direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 5, Name = "retro-wave", Description = "Modern 80s synthwave aesthetic portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, modern retro wave aesthetic, neon city night backdrop with soft pink and blue accents, soft diffused key light with balanced fill, natural skin tones, realistic skin texture, subtle rim light, shallow depth of field, neon bokeh background, 85mm lens, cinematic color grading, confident trendy expression, contemporary style with vintage flair, subtle film grain", NegativePromptTemplate = "harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 6, Name = "medical", Description = "Healthcare professional style", PromptTemplate = "{subject}, medical professional portrait of {gender} {ethnicity}, healthcare style, professional medical attire, trustworthy healthcare provider appearance, warm caring expression, clinical background", NegativePromptTemplate = "unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 7, Name = "night-out", Description = "Sophisticated evening portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, sophisticated night-out aesthetic, evening city lounge or street bokeh background, ambient street lighting only, soft side key light with gentle fill, no frontal key, no flash, natural skin tones, realistic skin texture, subtle rim light, cinematic color grading, confident social expression, subtle glamour, shallow depth of field, 85mm lens, subtle film grain", NegativePromptTemplate = "harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 8, Name = "entrepreneur", Description = "Entrepreneurial business style", PromptTemplate = "{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 9, Name = "startup", Description = "Startup professional style", PromptTemplate = "{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, healthy natural skin, even skin tone, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 10, Name = "tech-professional", Description = "Technology professional style", PromptTemplate = "{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 11, Name = "influencer", Description = "Social media influencer style", PromptTemplate = "{subject}, influencer portrait of {gender} {ethnicity}, engaging personality style, trendy professional appearance, charismatic expression", NegativePromptTemplate = "formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 12, Name = "digital-nomad", Description = "Digital nomad professional", PromptTemplate = "{subject}, digital nomad portrait of {gender} {ethnicity}, remote work professional style, casual modern attire, location-independent professional", NegativePromptTemplate = "formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 13, Name = "creative", Description = "Creative professional style", PromptTemplate = "{subject}, creative professional portrait of {gender} {ethnicity}, modern creative director vibe, stylish contemporary outfit, subtle colorful studio or gallery background, bright airy daylight, playful confident expression, clean editorial photography, head-and-shoulders framing, natural skin texture, minimal retouching, sharp focus, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            // Id 14: casual — prompt strengthened to force clothed output; model associates "casual" with shirtless at low guidance scale
            new Style { Id = 14, Name = "casual", Description = "Casual professional style", PromptTemplate = "{subject}, casual lifestyle portrait of {gender} {ethnicity}, wearing a casual t-shirt or henley or hoodie or crewneck sweater (fully clothed upper body), relaxed candid smile or laugh, outdoors (park/city street) or cozy home background, golden-hour natural light, relaxed posture (hands in pockets or open gesture), natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, bare torso, topless, no shirt, nude, undressed, exposed skin, skin showing, without shirt, uncovered chest, tank top, undershirt, muscle tank, sleeveless shirt", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 15, Name = "artistic", Description = "Artistic creative portrait", PromptTemplate = "{subject}, fine-art portrait photography of {gender} {ethnicity}, cinematic moody studio lighting, chiaroscuro, textured backdrop, cinematic color grading, contemplative expression, artistic composition, subtle film grain, head-and-shoulders framing, natural skin texture, minimal retouching, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 16, Name = "edgy-urban", Description = "Edgy urban style", PromptTemplate = "{subject}, edgy urban portrait of {gender} {ethnicity}, modern urban style, contemporary city fashion, bold confident expression", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, conservative formal, traditional business, bland conventional, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 17, Name = "glamour", Description = "Glamour portrait style", PromptTemplate = "{subject}, glamour portrait of {gender} {ethnicity}, elegant sophisticated style, polished glamorous appearance, high-end fashion aesthetic", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, casual simple, plain appearance, understated look, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 18, Name = "academic", Description = "Academic professional style", PromptTemplate = "{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution", NegativePromptTemplate = "blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            // Id 19: fitness — intentionally omits anti-nudity terms.
            // Design decision: fitness portraits legitimately feature athletic builds in sports/gym attire
            // (e.g., tank tops, sports bras). Adding "shirtless" or "bare chest" would cause the model
            // to over-clothe subjects in ways inconsistent with the fitness aesthetic.
            // Review this decision if the style is extended to non-athletic use cases.
            new Style { Id = 19, Name = "fitness", Description = "Fitness professional style", PromptTemplate = "{subject}, fitness professional portrait of {gender} {ethnicity}, athletic performance aesthetic, fitted athletic apparel, gym or wellness studio backdrop, energetic confident expression", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, sedentary look, unhealthy appearance, low energy, leather jacket, city street fashion, edgy-urban style, nightclub look, suit and tie, corporate boardroom, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Style { Id = 20, Name = "digital-native", Description = "Modern tech creator portrait", PromptTemplate = "{subject}, professional portrait of {gender} {ethnicity}, modern digital creator aesthetic, subtle RGB accent lighting, clean tech-inspired background, confident creative expression, contemporary casual style, soft purple and cyan color accents, approachable online personality", NegativePromptTemplate = "blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        };

        builder.Entity<Style>().HasData(styles);
    }
}
