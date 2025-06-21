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
    public DbSet<ModelCreationRequest> ModelCreationRequests { get; set; }
    public DbSet<UsageLog> UsageLogs { get; set; }
    
    // Subscription management
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    
    // Premium Package management
    public DbSet<PremiumPackage> PremiumPackages { get; set; }
    public DbSet<UserPackagePurchase> UserPackagePurchases { get; set; }

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

        // Seed initial styles
        builder.Entity<Style>().HasData(
            new Style { Id = 1, Name = "speaker", Description = "Confident presentation style for conference speakers and thought leaders", PromptTemplate = "{subject}, conference speaker portrait, composition: confident presentation-ready framing with authoritative presence, lighting: professional speaking lighting with clear visibility, color palette: trustworthy blues and confident tones, mood: knowledgeable, engaging and authoritative, technical details: conference-quality with excellent clarity, additional elements: speaking environment or professional backdrop, business professional attire, confident engaging expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, intimidating expression, unprofessional attire", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 2, Name = "casual", Description = "Relaxed lifestyle portrait with natural styling", PromptTemplate = "{subject}, casual lifestyle portrait, composition: rule of thirds with natural framing, lighting: golden hour natural sunlight with soft diffusion, color palette: warm earthy tones with vibrant accents, mood: relaxed, friendly and authentic, technical details: shot with 50mm lens at f/2.0, medium depth of field, additional elements: outdoor setting with natural elements, casual stylish clothing, genuine smile", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 3, Name = "creative", Description = "Artistic creative portrait with dynamic composition", PromptTemplate = "{subject}, artistic creative portrait, composition: dynamic asymmetrical framing with creative negative space, lighting: dramatic side lighting with colored gels and intentional shadows, color palette: bold contrasting colors with artistic color grading, mood: intriguing and expressive, technical details: shot with wide angle lens, creative perspective, high contrast, additional elements: artistic background elements, creative props or styling, unique fashion elements", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, boring, plain background, standard pose, conventional lighting", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 4, Name = "corporate", Description = "Executive corporate portrait with formal styling", PromptTemplate = "{subject}, executive corporate portrait, composition: formal centered composition with professional framing, lighting: classic Rembrandt lighting with soft fill, color palette: deep blues, grays and blacks with subtle accents, mood: authoritative, trustworthy and professional, technical details: shot with medium telephoto lens, optimal clarity and sharpness, additional elements: elegant business attire, office or branded environment subtly visible, power posture", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual attire, beach, party scene, inappropriate setting", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 5, Name = "linkedin", Description = "Optimized LinkedIn profile photo with professional appeal", PromptTemplate = "{subject}, optimized LinkedIn profile photo, composition: head and shoulders framing with balanced negative space above head, lighting: flattering soft light with subtle highlighting, color palette: professional neutral tones with complementary background, mood: approachable yet professional, technical details: 1000x1000 pixel square format, sharp focus on eyes, additional elements: simple clean background, professional but approachable expression, business casual attire", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, full body shot, distracting background, extreme filters, unprofessional setting", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 6, Name = "academic", Description = "Scholarly academic portrait with intellectual elements", PromptTemplate = "{subject}, scholarly academic portrait, composition: dignified framing with intellectual elements, lighting: soft even lighting with subtle gradient, color palette: rich traditional tones with subtle depth, mood: thoughtful, knowledgeable and authoritative, technical details: medium format quality, excellent clarity, additional elements: books, laboratory or campus environment, academic attire or professional clothing, scholarly posture", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 7, Name = "tech", Description = "Modern tech industry portrait with contemporary styling", PromptTemplate = "{subject}, modern tech industry portrait, composition: contemporary framing with technical elements, lighting: modern high-key lighting with subtle blue accents, color palette: tech blues and cool grays with vibrant accents, mood: innovative, forward-thinking and approachable, technical details: ultra-high definition, perfect clarity, additional elements: minimal tech environment, modern casual professional attire, confident engaged expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated technology, traditional office, formal suit", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 8, Name = "medical", Description = "Healthcare professional portrait with trustworthy appeal", PromptTemplate = "{subject}, healthcare professional portrait, composition: trustworthy frontal composition with medical context, lighting: clean even lighting with healthy glow, color palette: whites, blues and comforting tones, mood: compassionate, competent and reassuring, technical details: sharp focus throughout, excellent clarity, additional elements: medical attire or lab coat, stethoscope or medical environment, caring expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, inappropriate medical setting, casual vacation clothing", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 9, Name = "legal", Description = "Legal professional portrait with authoritative presence", PromptTemplate = "{subject}, legal professional portrait, composition: balanced formal composition with legal elements, lighting: classical portrait lighting with defined shadows, color palette: deep rich tones with mahogany and navy accents, mood: authoritative, trustworthy and dignified, technical details: perfect focus and formal composition, additional elements: legal books, office with wooden elements, formal suit, confident and serious expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, inappropriate attire, party scene", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 10, Name = "executive", Description = "Premium executive portrait with commanding presence", PromptTemplate = "{subject}, premium executive portrait, composition: powerful centered composition with prestigious elements, lighting: dramatic executive lighting with defined highlights, color palette: luxury tones with gold, navy and charcoal accents, mood: powerful, successful and commanding, technical details: medium format quality with perfect detail rendering, additional elements: luxury office environment, premium suit or executive attire, leadership pose and expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, unprofessional setting, low quality office", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 11, Name = "entrepreneur", Description = "Modern entrepreneur portrait with innovative energy", PromptTemplate = "{subject}, modern entrepreneur portrait, composition: confident three-quarter pose with dynamic energy, lighting: contemporary mixed lighting with natural and artificial sources, color palette: bold modern colors with tech-inspired gradients, mood: ambitious, innovative and approachable, technical details: crisp modern photography style, additional elements: startup environment or modern co-working space, smart casual or business casual attire, confident forward-thinking expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated office, overly formal suit, stiff pose", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 12, Name = "influencer", Description = "Social media influencer portrait with engaging appeal", PromptTemplate = "{subject}, social media influencer portrait, composition: engaging direct-to-camera angle with modern framing, lighting: bright even lighting with subtle ring light effect, color palette: vibrant contemporary colors with Instagram-worthy tones, mood: charismatic, relatable and engaging, technical details: high-definition social media optimized, additional elements: trendy background or lifestyle setting, fashionable modern clothing, warm engaging smile", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated fashion, corporate stiffness, overly formal setting", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 13, Name = "artistic", Description = "Fine art portrait with sophisticated aesthetic", PromptTemplate = "{subject}, fine art portrait, composition: artistic composition with painterly quality, lighting: dramatic chiaroscuro lighting with artistic shadows, color palette: rich artistic tones with gallery-worthy color grading, mood: sophisticated, contemplative and expressive, technical details: museum-quality detail and composition, additional elements: artistic background with textural elements, elegant styling, thoughtful artistic expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, commercial look, basic lighting, mundane setting", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 14, Name = "fitness", Description = "Health and fitness professional portrait with active energy", PromptTemplate = "{subject}, health and fitness professional portrait, composition: energetic pose showcasing vitality and strength, lighting: dynamic lighting that highlights muscle definition and healthy glow, color palette: vibrant energetic colors with natural skin tones, mood: motivated, healthy and inspiring, technical details: sharp action-ready photography, additional elements: fitness or wellness environment, athletic or activewear, confident energetic expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, sedentary pose, unhealthy appearance, formal business attire", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 15, Name = "consultant", Description = "Professional consultant portrait with expertise and approachability", PromptTemplate = "{subject}, professional consultant portrait, composition: confident advisory pose with approachable authority, lighting: professional consulting office lighting with subtle warmth, color palette: trustworthy blues and grays with warm accent tones, mood: knowledgeable, trustworthy and consultative, technical details: crisp professional quality with excellent clarity, additional elements: modern consulting environment or neutral professional background, business professional attire, advisor-like confident expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, overly formal pose, intimidating expression", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 16, Name = "startup", Description = "Clean, tech-savvy style perfect for tech founders", PromptTemplate = "{subject}, startup founder portrait, composition: clean modern framing with tech-forward aesthetic, lighting: bright even lighting with subtle blue tech accents, color palette: modern whites, grays and tech blues, mood: innovative, confident and forward-thinking, technical details: crisp contemporary photography, additional elements: minimal tech environment or clean office space, smart casual modern attire, confident entrepreneurial expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated office, overly formal suit, cluttered background", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 17, Name = "dating", Description = "Friendly, attractive style optimized for dating profiles", PromptTemplate = "{subject}, dating profile portrait, composition: warm inviting framing with natural charm, lighting: flattering soft natural light with golden hour warmth, color palette: warm inviting tones with natural skin enhancement, mood: genuine, approachable and attractive, technical details: dating-app optimized with authentic appeal, additional elements: casual attractive clothing, warm genuine smile, approachable friendly expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, overly formal attire, intimidating expression, fake smile", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 18, Name = "luxury", Description = "Upscale, luxury-brand inspired portrait with rich tones", PromptTemplate = "{subject}, luxury brand portrait, composition: sophisticated upscale framing with premium aesthetic, lighting: dramatic luxury lighting with rich shadows and highlights, color palette: rich luxury tones with gold, deep blues and sophisticated neutrals, mood: affluent, sophisticated and exclusive, technical details: high-end fashion photography quality, additional elements: luxury environment or premium backdrop, designer clothing or elegant attire, confident sophisticated expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, cheap clothing, budget setting, casual environment", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 19, Name = "content_creator", Description = "Bold style-forward portrait ideal for Instagram and social media", PromptTemplate = "{subject}, content creator portrait, composition: dynamic social media optimized framing, lighting: vibrant ring light with social media perfect illumination, color palette: bold trendy colors with Instagram-worthy vibes, mood: creative, confident and camera-ready, technical details: social media optimized with trending aesthetic, additional elements: trendy modern background, fashionable content creator styling, engaging camera-ready expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated fashion, boring background, corporate stiffness", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Style { Id = 20, Name = "author", Description = "Warm-toned bookish portrait with thoughtful intellectual appeal", PromptTemplate = "{subject}, author portrait, composition: thoughtful intellectual framing with literary elements, lighting: warm library lighting with cozy book-friendly ambiance, color palette: warm earth tones with literary browns and rich textures, mood: thoughtful, intellectual and approachable, technical details: editorial quality with literary sophistication, additional elements: bookshelves or writing desk backdrop, smart casual or tweed styling, contemplative thoughtful expression", NegativePromptTemplate = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, modern tech environment, overly casual attire, distracting background", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );

        // Seed subscription plans
        builder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan 
            { 
                Id = "basic-plan",
                Name = "Basic",
                Description = "Perfect for casual users who want to enhance their photos",
                Price = 0.00m,
                BillingPeriod = "monthly",
                ImagesPerMonth = 3,
                CanTrainCustomModels = false,
                CanBatchGenerate = false,
                HighResolutionOutput = false,
                MaxTrainingImages = 0,
                MaxStylesAccess = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan 
            { 
                Id = "premium-monthly",
                Name = "Premium",
                Description = "Ideal for professionals who need custom AI models and advanced features",
                Price = 19.99m,
                BillingPeriod = "monthly",
                ImagesPerMonth = 50,
                CanTrainCustomModels = true,
                CanBatchGenerate = true,
                HighResolutionOutput = true,
                MaxTrainingImages = 20,
                MaxStylesAccess = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan 
            { 
                Id = "premium-yearly",
                Name = "Premium (Yearly)",
                Description = "Premium features with 2 months free when billed annually",
                Price = 199.99m,
                BillingPeriod = "yearly",
                ImagesPerMonth = 50,
                CanTrainCustomModels = true,
                CanBatchGenerate = true,
                HighResolutionOutput = true,
                MaxTrainingImages = 20,
                MaxStylesAccess = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan 
            { 
                Id = "pro-monthly",
                Name = "Pro",
                Description = "For businesses and power users who need unlimited access",
                Price = 49.99m,
                BillingPeriod = "monthly",
                ImagesPerMonth = 200,
                CanTrainCustomModels = true,
                CanBatchGenerate = true,
                HighResolutionOutput = true,
                MaxTrainingImages = 50,
                MaxStylesAccess = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan 
            { 
                Id = "pro-yearly",
                Name = "Pro (Yearly)",
                Description = "Pro features with 2 months free when billed annually",
                Price = 499.99m,
                BillingPeriod = "yearly",
                ImagesPerMonth = 200,
                CanTrainCustomModels = true,
                CanBatchGenerate = true,
                HighResolutionOutput = true,
                MaxTrainingImages = 50,
                MaxStylesAccess = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed premium packages
        builder.Entity<PremiumPackage>().HasData(
            new PremiumPackage
            {
                Id = 1,
                Name = "Quick Shot",
                Credits = 15,
                Price = 9.99m,
                MaxStyles = 5,
                MaxImagesPerStyle = 2,
                Description = "Generate 10 professional photos with 5 different styles",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new PremiumPackage
            {
                Id = 2,
                Name = "Professional",
                Credits = 25,
                Price = 19.99m,
                MaxStyles = 5,
                MaxImagesPerStyle = 4,
                Description = "Generate 20 professional photos with 5 different styles",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new PremiumPackage
            {
                Id = 3,
                Name = "Premium Studio",
                Credits = 45,
                Price = 34.99m,
                MaxStyles = 8,
                MaxImagesPerStyle = 5,
                Description = "Generate 40 professional photos with 8 different styles",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new PremiumPackage
            {
                Id = 4,
                Name = "Ultimate",
                Credits = 105,
                Price = 54.99m,
                MaxStyles = 20,
                MaxImagesPerStyle = 5,
                Description = "Generate 100 professional photos with 20 different styles",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}