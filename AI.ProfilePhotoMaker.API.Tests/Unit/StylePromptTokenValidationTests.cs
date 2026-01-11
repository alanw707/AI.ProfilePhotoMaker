using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

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
}
