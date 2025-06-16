using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class ExternalLoginDto
{
    [Required]
    public string Provider { get; set; } = string.Empty;
    
    public string? ReturnUrl { get; set; }
}

public class ExternalLoginCallbackDto
{
    [Required]
    public string Provider { get; set; } = string.Empty;
    
    [Required]
    public string Code { get; set; } = string.Empty;
    
    public string? State { get; set; }
    
    public string? ReturnUrl { get; set; }
}

public class ExternalLoginInfoDto
{
    public string Provider { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
}