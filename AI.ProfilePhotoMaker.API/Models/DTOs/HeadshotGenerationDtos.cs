using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class HeadshotGenerationRequestDto
{
    [Required]
    public string ImageStoragePath { get; set; } = string.Empty;

    public string Style { get; set; } = "linkedin";

    public string Background { get; set; } = "auto";

    public string PackageCode { get; set; } = "free_preview";

    [Range(1, 9, ErrorMessage = "Instant headshot generation supports between 1 and 9 candidates per request.")]
    public int NumOutputs { get; set; } = 1;

    public bool IsRegeneration { get; set; }

    public int? ReusedPreviewProcessedImageId { get; set; }

    [StringLength(64)]
    public string? UseCaseCode { get; set; }

    [StringLength(64)]
    public string? RecipeCode { get; set; }

    public string? TurnstileToken { get; set; }

    [StringLength(100)]
    public string? ClientRequestId { get; set; }
}

public class HeadshotGenerationResponseDto
{
    public bool Success { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public int ProcessedImageId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public int CreditsCost { get; set; }
    public int RemainingCredits { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public List<HeadshotCandidateDto> Candidates { get; set; } = new();
}

public class HeadshotCandidateDto
{
    public string ImageUrl { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public int ProcessedImageId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? UseCaseCode { get; set; }
    public string? RecipeCode { get; set; }
    public string? Label { get; set; }
}

public class ResumableHeadshotPreviewDto
{
    public int ProcessedImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string SourceStoragePath { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasRawPreview { get; set; }
    public bool CanPromotePreview { get; set; }
    public string? ActivePackageCode { get; set; }
    public int RemainingCandidateCount { get; set; }
    public HeadshotCandidateDto? PromotedCandidate { get; set; }
    public string? Message { get; set; }
}
