using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public sealed record PackageGenerationAllowance(bool Allowed, string? FailureCode, string? FailureMessage)
{
    public static PackageGenerationAllowance Allow() => new(true, null, null);
    public static PackageGenerationAllowance Deny(string message) => new(false, "PackageEntitlementRequired", message);
    public static PackageGenerationAllowance Deny(string failureCode, string message) => new(false, failureCode, message);
}

/// <summary>
/// Deep Package entitlement policy module for Instant headshot generation.
/// Interface: callers provide package code, requested candidate count, regeneration flag, and optional entitlement.
/// Implementation owns the rules for Free Preview, paid candidate generation, and refinement allowance.
/// </summary>
public static class PackageEntitlementPolicy
{
    public static PackageGenerationAllowance CheckGenerationAllowance(
        string packageCode,
        int requestedCandidateCount,
        bool isRegeneration,
        UserPackageEntitlement? entitlement)
    {
        if (packageCode == "free_preview")
        {
            if (requestedCandidateCount == 1 && !isRegeneration)
            {
                return PackageGenerationAllowance.Allow();
            }

            return PackageGenerationAllowance.Deny(
                "FreePreviewExhausted",
                "Free Preview includes one image. Unlock Starter to generate more.");
        }

        if (entitlement == null)
        {
            return PackageGenerationAllowance.Deny(isRegeneration
                ? "This package has no refinements remaining."
                : "Choose or unlock a profile photo package before generating these candidates.");
        }

        if (isRegeneration)
        {
            return entitlement.RemainingRefinements > 0
                ? PackageGenerationAllowance.Allow()
                : PackageGenerationAllowance.Deny("This package has no refinements remaining.");
        }

        return entitlement.RemainingPackageUses > 0 && entitlement.RemainingCandidates >= requestedCandidateCount
            ? PackageGenerationAllowance.Allow()
            : PackageGenerationAllowance.Deny("Choose or unlock a profile photo package before generating these candidates.");
    }
}
