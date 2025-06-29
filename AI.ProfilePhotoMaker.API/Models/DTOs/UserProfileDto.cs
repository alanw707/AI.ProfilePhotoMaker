using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public string? TrainedModelId { get; set; }
    public string? TrainedModelVersionId { get; set; }
    public DateTime? ModelTrainedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalProcessedImages { get; set; }
}

public class CreateUserProfileDto
{
    [Required]
    [StringLength(50)]
    public string? FirstName { get; set; }
    [Required]
    [StringLength(50)]
    public string? LastName { get; set; }
    [StringLength(50)]
    public string? Gender { get; set; }
    [StringLength(50)]
    public string? Ethnicity { get; set; }
}

public class UpdateUserProfileDto
{
    [Required]
    [StringLength(50)]
    public string? FirstName { get; set; }
    [Required]
    [StringLength(50)]
    public string? LastName { get; set; }
    [StringLength(50)]
    public string? Gender { get; set; }
    [StringLength(50)]
    public string? Ethnicity { get; set; }
}

public class UploadImagesDto
{
    [Required]
    public List<IFormFile> Images { get; set; } = new();
    [StringLength(50)]
    public string? FirstName { get; set; }
    [StringLength(50)]
    public string? LastName { get; set; }
    [StringLength(50)]
    public string? Gender { get; set; }
    [StringLength(50)]
    public string? Ethnicity { get; set; }
    // Add this flag to indicate if upload is for training
    public bool ForTraining { get; set; } = false;
}