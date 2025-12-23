using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class RequireTrueAttribute : ValidationAttribute
{
    public RequireTrueAttribute()
    {
        ErrorMessage = "This field must be true.";
    }

    public override bool IsValid(object? value)
    {
        return value is bool boolValue && boolValue;
    }
}
