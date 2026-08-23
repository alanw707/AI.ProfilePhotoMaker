using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class PackageEntitlementPolicyTests
{
    [Fact]
    public void CheckGenerationAllowance_AllowsSingleFreePreview()
    {
        var result = PackageEntitlementPolicy.CheckGenerationAllowance("free_preview", 1, false, null);

        Assert.True(result.Allowed);
        Assert.Null(result.FailureCode);
    }

    [Fact]
    public void CheckGenerationAllowance_DeniesFreePreviewRegeneration()
    {
        var result = PackageEntitlementPolicy.CheckGenerationAllowance("free_preview", 1, true, null);

        Assert.False(result.Allowed);
        Assert.Equal("FreePreviewExhausted", result.FailureCode);
        Assert.DoesNotContain("unlock a profile photo package", result.FailureMessage);
    }

    [Fact]
    public void CheckGenerationAllowance_AllowsPaidCandidatesWhenPackageUseAndCandidatesRemain()
    {
        var entitlement = new UserPackageEntitlement
        {
            RemainingPackageUses = 1,
            RemainingCandidates = 3,
            RemainingRefinements = 0
        };

        var result = PackageEntitlementPolicy.CheckGenerationAllowance("starter_package", 3, false, entitlement);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckGenerationAllowance_DeniesPaidCandidatesWhenCandidateAllowanceTooSmall()
    {
        var entitlement = new UserPackageEntitlement
        {
            RemainingPackageUses = 1,
            RemainingCandidates = 2,
            RemainingRefinements = 0
        };

        var result = PackageEntitlementPolicy.CheckGenerationAllowance("starter_package", 3, false, entitlement);

        Assert.False(result.Allowed);
        Assert.Equal("PackageEntitlementRequired", result.FailureCode);
    }

    [Fact]
    public void CheckGenerationAllowance_UsesRefinementAllowanceForRegeneration()
    {
        var entitlement = new UserPackageEntitlement
        {
            RemainingPackageUses = 0,
            RemainingCandidates = 0,
            RemainingRefinements = 1
        };

        var result = PackageEntitlementPolicy.CheckGenerationAllowance("pro_package", 1, true, entitlement);

        Assert.True(result.Allowed);
    }
}
