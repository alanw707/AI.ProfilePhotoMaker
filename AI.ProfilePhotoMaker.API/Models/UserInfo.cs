namespace AI.ProfilePhotoMaker.API.Models;

/// <summary>
/// Represents user information for AI processing
/// </summary>
public class UserInfo
{
    /// <summary>
    /// The user's gender (optional)
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// The user's ethnicity (optional)
    /// </summary>
    public string? Ethnicity { get; set; }
    
    /// <summary>
    /// Additional attributes for AI processing
    /// </summary>
    public Dictionary<string, string>? Attributes { get; set; }
}