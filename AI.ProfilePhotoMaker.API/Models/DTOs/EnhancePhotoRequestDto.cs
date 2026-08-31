using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class EnhancePhotoRequestDto : IValidatableObject
{
    public string? ImageUrl { get; set; }

    public string? ImageStoragePath { get; set; }

    public string? EnhancementType { get; set; } = "professional";

    public string? CustomPrompt { get; set; }

    public bool IsDeblurRequest { get; set; } = false;
    public double DeblurStrength { get; set; } = 0.5; // Default deblur strength
    public string? PhotoId { get; set; } // To link to a specific photo if needed

    // Optional: Cloudflare Turnstile verification token to guard expensive endpoints.
    public string? TurnstileToken { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ImageStoragePath))
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(ImageUrl))
        {
            yield return new ValidationResult(
                "Either imageStoragePath or imageUrl is required for photo enhancement",
                new[] { nameof(ImageStoragePath), nameof(ImageUrl) });
        }
        else if (!new UrlAttribute().IsValid(ImageUrl))
        {
            yield return new ValidationResult("Image URL must be a valid URL", new[] { nameof(ImageUrl) });
        }
    }
}
