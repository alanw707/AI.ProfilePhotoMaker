namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalProcessedImages { get; set; }
}

public class CreateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
}

public class UpdateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
}

public class UploadImagesDto
{
    public List<IFormFile> Images { get; set; } = new();
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
}