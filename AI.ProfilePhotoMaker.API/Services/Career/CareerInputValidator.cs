using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Normalised, validated profile facts ready to store.</summary>
public sealed record ValidProfileFacts(
    string CurrentTitle,
    string? Industry,
    int? YearsExperience,
    string? Location,
    string? Summary,
    List<string> Skills,
    List<string> Highlights,
    string? WorkArrangement);

public sealed record ValidGoalFacts(
    string TargetRole,
    string? TargetLocation,
    string? WorkArrangement,
    int? DesiredPayMin,
    int? DesiredPayMax,
    int? WeeklyEffortHours);

/// <summary>
/// Validates and normalises manual career input. Limits match
/// docs/career/api-profile-goal.md; field keys are camelCase request names so the
/// UI can attach messages to its controls.
/// </summary>
public static class CareerInputValidator
{
    public const int MaxShortText = 120;
    public const int MaxSummary = 2000;
    public const int MaxSkills = 50;
    public const int MaxSkillLength = 80;
    public const int MaxHighlights = 20;
    public const int MaxHighlightLength = 300;
    public const int MaxPay = 1_000_000;

    public static (ValidProfileFacts? Facts, Dictionary<string, string> Errors) Validate(CareerProfileRequest request)
    {
        var errors = new Dictionary<string, string>();

        var title = Required(request.CurrentTitle, "currentTitle", MaxShortText, errors);
        var industry = Optional(request.Industry, "industry", MaxShortText, errors);
        var location = Optional(request.Location, "location", MaxShortText, errors);
        var summary = Optional(request.Summary, "summary", MaxSummary, errors);
        var arrangement = Arrangement(request.WorkArrangement, errors);
        Range(request.YearsExperience, 0, 60, "yearsExperience", errors);
        var skills = List(request.Skills, "skills", MaxSkills, MaxSkillLength, deduplicate: true, errors);
        var highlights = List(request.Highlights, "highlights", MaxHighlights, MaxHighlightLength, deduplicate: false, errors);
        Confirmed(request.Confirmed, errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidProfileFacts(title!, industry, request.YearsExperience, location, summary, skills, highlights, arrangement), errors);
    }

    public static (ValidGoalFacts? Goal, Dictionary<string, string> Errors) Validate(CareerGoalRequest request)
    {
        var errors = new Dictionary<string, string>();

        var role = Required(request.TargetRole, "targetRole", MaxShortText, errors);
        var location = Optional(request.TargetLocation, "targetLocation", MaxShortText, errors);
        var arrangement = Arrangement(request.WorkArrangement, errors);
        Range(request.DesiredPayMin, 0, MaxPay, "desiredPayMin", errors);
        Range(request.DesiredPayMax, 0, MaxPay, "desiredPayMax", errors);
        if (request.DesiredPayMin is { } min && request.DesiredPayMax is { } max && max < min && !errors.ContainsKey("desiredPayMax"))
        {
            errors["desiredPayMax"] = "Maximum pay must be at least the minimum.";
        }
        Range(request.WeeklyEffortHours, 1, 40, "weeklyEffortHours", errors);
        Confirmed(request.Confirmed, errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidGoalFacts(role!, location, arrangement, request.DesiredPayMin, request.DesiredPayMax, request.WeeklyEffortHours), errors);
    }

    private static string? Required(string? value, string field, int maxLength, Dictionary<string, string> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors[field] = "Required.";
            return null;
        }
        if (trimmed.Length > maxLength)
        {
            errors[field] = $"Use {maxLength} characters or fewer.";
            return null;
        }
        return trimmed;
    }

    /// <summary>Blank optional text is stored as null rather than an empty string.</summary>
    private static string? Optional(string? value, string field, int maxLength, Dictionary<string, string> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length > maxLength)
        {
            errors[field] = $"Use {maxLength} characters or fewer.";
            return null;
        }
        return trimmed;
    }

    private static string? Arrangement(string? value, Dictionary<string, string> errors)
    {
        var normalised = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalised))
        {
            return null;
        }
        if (!CareerWorkArrangement.All.Contains(normalised))
        {
            errors["workArrangement"] = "Choose on-site, hybrid, remote or flexible.";
            return null;
        }
        return normalised;
    }

    private static void Range(int? value, int min, int max, string field, Dictionary<string, string> errors)
    {
        if (value is { } v && (v < min || v > max))
        {
            errors[field] = $"Enter a number from {min:N0} to {max:N0}.";
        }
    }

    private static List<string> List(
        List<string?>? values,
        string field,
        int maxItems,
        int maxLength,
        bool deduplicate,
        Dictionary<string, string> errors)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in values ?? new List<string?>())
        {
            var item = raw?.Trim();
            if (string.IsNullOrEmpty(item))
            {
                continue;
            }
            if (item.Length > maxLength)
            {
                errors[field] = $"Each item must be {maxLength} characters or fewer.";
                return result;
            }
            if (deduplicate && !seen.Add(item))
            {
                continue;
            }
            result.Add(item);
        }

        if (result.Count > maxItems)
        {
            errors[field] = $"Add up to {maxItems} items.";
        }
        return result;
    }

    private static void Confirmed(bool confirmed, Dictionary<string, string> errors)
    {
        if (!confirmed)
        {
            errors["confirmed"] = "Confirm these facts are accurate before saving.";
        }
    }
}
