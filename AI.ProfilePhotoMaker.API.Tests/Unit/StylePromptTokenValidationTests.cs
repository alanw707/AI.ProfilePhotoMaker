using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

/// <summary>
/// Validates that style prompt templates contain required tokens for proper generation.
/// These tests ensure migrations don't accidentally remove critical template variables.
/// </summary>
public class StylePromptTokenValidationTests
{
    private static readonly string[] RequiredTokens = { "{subject}", "{gender}", "{ethnicity}" };

    private static readonly string[] ProfessionalStyleNames =
    {
        "executive", "linkedin", "startup", "tech-professional", "entrepreneur", "academic"
    };

    [Fact]
    public void ProfessionalStyles_SeedData_ContainRequiredTokens()
    {
        // Arrange - Get seed data from ApplicationDbContext
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TokenValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Act & Assert
        foreach (var styleName in ProfessionalStyleNames)
        {
            var style = context.Styles.FirstOrDefault(s => s.Name == styleName);

            Assert.NotNull(style);
            Assert.False(string.IsNullOrWhiteSpace(style.PromptTemplate),
                $"Style '{styleName}' has empty PromptTemplate");

            foreach (var token in RequiredTokens)
            {
                Assert.True(style.PromptTemplate.Contains(token),
                    $"Style '{styleName}' PromptTemplate missing required token: {token}");
            }
        }
    }

    [Fact]
    public void ProfessionalStyles_SeedData_HaveNonEmptyNegativePrompts()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NegativePromptValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Act & Assert
        foreach (var styleName in ProfessionalStyleNames)
        {
            var style = context.Styles.FirstOrDefault(s => s.Name == styleName);

            Assert.NotNull(style);
            Assert.False(string.IsNullOrWhiteSpace(style.NegativePromptTemplate),
                $"Style '{styleName}' has empty NegativePromptTemplate");

            // Verify skin realism terms are present (but not overly aggressive ones)
            Assert.True(style.NegativePromptTemplate.Contains("waxy skin"),
                $"Style '{styleName}' NegativePromptTemplate missing 'waxy skin' realism guard");
            Assert.True(style.NegativePromptTemplate.Contains("plastic skin"),
                $"Style '{styleName}' NegativePromptTemplate missing 'plastic skin' realism guard");
        }
    }

    [Fact]
    public void ProfessionalStyles_SeedData_HaveHealthySkinGuidance()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"HealthySkinValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Act & Assert
        foreach (var styleName in ProfessionalStyleNames)
        {
            var style = context.Styles.FirstOrDefault(s => s.Name == styleName);

            Assert.NotNull(style);

            // Verify positive skin guidance is present (added in SoftenSkinRealismConstraints migration)
            Assert.True(style.PromptTemplate.Contains("healthy natural skin"),
                $"Style '{styleName}' PromptTemplate missing 'healthy natural skin' guidance");
            Assert.True(style.PromptTemplate.Contains("even skin tone"),
                $"Style '{styleName}' PromptTemplate missing 'even skin tone' guidance");
        }
    }

    [Fact]
    public void ProfessionalStyles_SeedData_DoNotContainOverlyAggressiveSkinTerms()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AggressiveTermsValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // These terms were removed in SoftenSkinRealismConstraints migration
        // because they caused dry/old/wrinkled appearance
        var removedAggressiveTerms = new[] { "poreless skin", "exaggerated wrinkles", "overly deep wrinkles" };

        // Act & Assert
        foreach (var styleName in ProfessionalStyleNames)
        {
            var style = context.Styles.FirstOrDefault(s => s.Name == styleName);

            Assert.NotNull(style);

            foreach (var term in removedAggressiveTerms)
            {
                Assert.False(style.NegativePromptTemplate.Contains(term),
                    $"Style '{styleName}' NegativePromptTemplate still contains aggressive term '{term}' that should have been removed");
            }
        }
    }

    [Fact]
    public void AllActiveStyles_HaveValidPromptTemplates()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AllStylesValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var activeStyles = context.Styles.Where(s => s.IsActive).ToList();

        // Assert
        Assert.NotEmpty(activeStyles);

        foreach (var style in activeStyles)
        {
            Assert.False(string.IsNullOrWhiteSpace(style.PromptTemplate),
                $"Active style '{style.Name}' (ID: {style.Id}) has empty PromptTemplate");
            Assert.False(string.IsNullOrWhiteSpace(style.NegativePromptTemplate),
                $"Active style '{style.Name}' (ID: {style.Id}) has empty NegativePromptTemplate");
        }
    }

    [Fact]
    public void AllActiveStyles_SeedData_HaveAntiNudityGuards()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AntiNudityValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Styles intentionally excluded from anti-nudity requirement (documented design decisions):
        // - "fresh": bare shoulders allowed by design (clean energetic refreshed aesthetic)
        // - "fitness": athletic build portraits legitimately include sports/gym attire that may be form-fitting;
        //   adding shirtless/bare chest guards causes over-clothing inconsistent with the fitness aesthetic.
        //   See ApplicationDbContext.SeedStyles() comment for full rationale.
        var stylesAllowingExposure = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "fresh", "fitness" };

        var antiNudityTerms = new[] { "shirtless", "bare chest", "topless", "nude", "undressed" };

        // Act
        var activeStyles = context.Styles.Where(s => s.IsActive).ToList();

        // Assert
        Assert.NotEmpty(activeStyles);

        foreach (var style in activeStyles)
        {
            if (stylesAllowingExposure.Contains(style.Name))
                continue; // These styles intentionally omit anti-nudity terms

            var hasAntiNudityTerm = antiNudityTerms.Any(term =>
                style.NegativePromptTemplate.Contains(term, StringComparison.OrdinalIgnoreCase));

            Assert.True(hasAntiNudityTerm,
                $"Style '{style.Name}' (ID: {style.Id}) NegativePromptTemplate missing anti-nudity guard. " +
                $"Expected at least one of: {string.Join(", ", antiNudityTerms)}");
        }
    }

    [Fact]
    public void AllActiveStyles_SeedData_HaveQualityBaseline()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"QualityBaselineValidation_{Guid.NewGuid()}")
            .Options;

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Full quality baseline terms enforced across all active styles.
        // If a term is intentionally removed from a style, update this list and document the reason.
        var requiredQualityTerms = new[]
        {
            "waxy skin",
            "plastic skin",
            "airbrushed skin",
            "over-smoothed skin",
            "beauty filter",
            "heavy retouching"
        };

        // Act
        var activeStyles = context.Styles.Where(s => s.IsActive).ToList();

        // Assert
        Assert.NotEmpty(activeStyles);

        foreach (var style in activeStyles)
        {
            foreach (var term in requiredQualityTerms)
            {
                Assert.True(style.NegativePromptTemplate.Contains(term, StringComparison.OrdinalIgnoreCase),
                    $"Style '{style.Name}' (ID: {style.Id}) NegativePromptTemplate missing quality baseline term '{term}'");
            }
        }
    }
}
