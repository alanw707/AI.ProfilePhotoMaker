using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Data;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(string userId);
    Task AddAsync(UserProfile profile);
    Task UpdateAsync(UserProfile profile);
    Task DeleteAsync(UserProfile profile);
}