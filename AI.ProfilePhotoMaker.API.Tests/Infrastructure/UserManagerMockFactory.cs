using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Infrastructure;

public static class UserManagerMockFactory
{
    public static Mock<UserManager<ApplicationUser>> Create(MockBehavior behavior = MockBehavior.Loose)
    {
        var store = new Mock<IUserStore<ApplicationUser>>(behavior);
        var options = Options.Create(new IdentityOptions());
        var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>(behavior);
        var userValidators = new[] { new Mock<IUserValidator<ApplicationUser>>(behavior).Object };
        var passwordValidators = new[] { new Mock<IPasswordValidator<ApplicationUser>>(behavior).Object };
        var keyNormalizer = new Mock<ILookupNormalizer>(behavior);
        var errors = new IdentityErrorDescriber();
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>(behavior);

        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            options,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            keyNormalizer.Object,
            errors,
            services,
            logger.Object);
    }
}

