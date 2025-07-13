using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Data;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly ApplicationDbContext _context;

    public UserProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByUserIdAsync(string userId)
    {
        return await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task AddAsync(UserProfile profile)
    {
        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        _context.UserProfiles.Update(profile);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserProfile profile)
    {
        _context.UserProfiles.Remove(profile);
        await _context.SaveChangesAsync();
    }
}