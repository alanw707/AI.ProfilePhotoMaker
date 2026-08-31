using System.ComponentModel.DataAnnotations;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Tests.Models;

public class EnhancePhotoRequestDtoTests
{
    [Fact]
    public void Validate_AllowsStoragePathWhenLegacyDisplayUrlIsRelative()
    {
        var dto = new EnhancePhotoRequestDto
        {
            ImageStoragePath = "uploads/source.jpg",
            ImageUrl = "/profile-images/uploads/source.jpg"
        };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);

        Assert.True(isValid, string.Join("; ", results.Select(result => result.ErrorMessage)));
    }
}
