namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class OutcomePackageDefinitionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public int? InternalCreditPackageId { get; set; }
    public int IncludedCandidateCount { get; set; }
    public int IncludedRefinementCount { get; set; }
    public int IncludedPremiumAugmentationCount { get; set; }
    public bool IncludesPlatformExportKit { get; set; }
    public bool IncludesScoreDelta { get; set; }
    public int DisplayOrder { get; set; }
    public string[] Highlights { get; set; } = Array.Empty<string>();
}

public class UserPackageEntitlementDto
{
    public int Id { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RemainingPackageUses { get; set; }
    public int RemainingCandidates { get; set; }
    public int RemainingRefinements { get; set; }
    public int RemainingPremiumAugmentations { get; set; }
    public bool PlatformExportKitAvailable { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ProfilePhotoScoreDto
{
    public int OverallScore { get; set; }
    public string RatingLabel { get; set; } = string.Empty;
    public List<ProfilePhotoSubscoreDto> Subscores { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string Guidance { get; set; } = string.Empty;
}

public class ProfilePhotoSubscoreDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public class PlatformExportOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string FileNameSuffix { get; set; } = string.Empty;
}

public class CreatePlatformExportPackageRequestDto
{
    public int ProcessedImageId { get; set; }
    public string[] ExportCodes { get; set; } = Array.Empty<string>();
    public int ZoomPercent { get; set; } = 100;
    public int RotateDegrees { get; set; } = 0;
    public int BrightnessPercent { get; set; } = 100;
    public int ContrastPercent { get; set; } = 100;
    public int SharpnessPercent { get; set; } = 100;
    public int CropOffsetXPercent { get; set; }
    public int CropOffsetYPercent { get; set; }
}
