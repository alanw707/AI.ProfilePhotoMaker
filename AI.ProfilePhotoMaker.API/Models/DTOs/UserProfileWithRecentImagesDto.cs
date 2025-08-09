using System.Collections.Generic;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class UserProfileWithRecentImagesDto
{
    public UserProfileDto Profile { get; set; } = null!;
    public List<ProcessedImageDto> RecentImages { get; set; } = new();
    public int TotalImageCount { get; set; }
}


