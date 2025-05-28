using Microsoft.AspNetCore.Identity;

namespace AI.ProfilePhotoMaker.API.Models;

public class ApplicationUser : IdentityUser
{
    // Properties
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Default parameterless constructor - required by EF Core
    public ApplicationUser() : base()
    {
    }

    // Optional: Parameterized constructor
    public ApplicationUser(string userName, string email, string firstName, string lastName) : base(userName)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }
}