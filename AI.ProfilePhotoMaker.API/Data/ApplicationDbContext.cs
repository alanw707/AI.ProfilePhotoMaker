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

    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<ProcessedImage> ProcessedImages { get; set; }
    public DbSet<Style> Styles { get; set; }
    public DbSet<UserStyleSelection> UserStyleSelections { get; set; }
    public DbSet<ModelCreationRequest> ModelCreationRequests { get; set; }
    public DbSet<UsageLog> UsageLogs { get; set; }
    
    // Subscription management
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    
    // Premium Package management (legacy)
    public DbSet<PremiumPackage> PremiumPackages { get; set; }
    public DbSet<UserPackagePurchase> UserPackagePurchases { get; set; }
    
    // Credit Package management (new unified system)
    public DbSet<CreditPackage> CreditPackages { get; set; }
    public DbSet<CreditPurchase> CreditPurchases { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure relationships
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId);

        builder.Entity<ProcessedImage>()
            .HasOne(i => i.UserProfile)
            .WithMany(p => p.ProcessedImages)
            .HasForeignKey(i => i.UserProfileId);

        // Configure UsageLog relationships
        builder.Entity<UsageLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId);

        builder.Entity<UserProfile>()
            .HasMany(p => p.UsageLogs)
            .WithOne()
            .HasForeignKey(l => l.UserId)
            .HasPrincipalKey(p => p.UserId);

        // Configure Style entity
        builder.Entity<Style>()
            .HasIndex(s => s.Name)
            .IsUnique();

        // Configure UserStyleSelection relationships
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
            .IsUnique();

        // Configure Subscription relationships
        builder.Entity<Subscription>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);

        builder.Entity<Subscription>()
            .HasOne(s => s.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PlanId);

        // Configure PaymentTransaction relationships
        builder.Entity<PaymentTransaction>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);

        builder.Entity<PaymentTransaction>()
            .HasOne(t => t.Subscription)
            .WithMany()
            .HasForeignKey(t => t.SubscriptionId);

        // Configure precision for decimal values
        builder.Entity<SubscriptionPlan>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Entity<PaymentTransaction>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);

        // Configure PremiumPackage relationships and constraints
        builder.Entity<PremiumPackage>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.Entity<PremiumPackage>()
            .HasIndex(p => p.Name)
            .IsUnique();

        // Configure UserPackagePurchase relationships
        builder.Entity<UserPackagePurchase>()
            .HasOne(p => p.Package)
            .WithMany(pkg => pkg.Purchases)
            .HasForeignKey(p => p.PackageId);

        builder.Entity<UserPackagePurchase>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId);

        builder.Entity<UserPackagePurchase>()
            .Property(p => p.AmountPaid)
            .HasPrecision(10, 2);

        // Configure CreditPackage relationships and constraints
        builder.Entity<CreditPackage>()
            .Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.Entity<CreditPackage>()
            .HasIndex(p => p.Name)
            .IsUnique();

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
            .Property(p => p.AmountPaid)
            .HasPrecision(10, 2);

        // Seed credit packages (new unified system)
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
                BonusCredits = 100, // Generous bonus
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new CreditPackage
            {
                Id = 4,
                Name = "Enterprise Pack",
                Credits = 750,
                Price = 79.99m,
                Description = "Maximum credits for agencies and enterprises",
                DisplayOrder = 4,
                BonusCredits = 250, // Excellent value
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}