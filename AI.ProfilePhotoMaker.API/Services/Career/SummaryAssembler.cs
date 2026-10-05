using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Builds a short bio and a long summary from a confirmed profile version (ADR 0019). Pure and deterministic:
/// every line cites the facts it uses, fact text is copied verbatim (a number appears only if the fact has it), and
/// nothing found inside a fact is ever interpreted. Section totals never exceed 300 / 1200 characters.
/// </summary>
public static class SummaryAssembler
{
    private const int MaxSkillsInLine = 8;

    public static ResumeDraft Assemble(CareerProfileVersion profile)
    {
        var shortLines = Build(profile, ResumeLimits.ShortSummaryMax, includeSummary: false, includeHighlights: false, skillCap: 3);
        var longLines = Build(profile, ResumeLimits.LongSummaryMax, includeSummary: true, includeHighlights: true, skillCap: MaxSkillsInLine);
        return new ResumeDraft(
            new[] { new ResumeSection(ResumeSectionKeys.Short, shortLines), new ResumeSection(ResumeSectionKeys.Long, longLines) },
            Array.Empty<ResumeQuestion>());
    }

    private static List<ResumeLine> Build(CareerProfileVersion profile, int budget, bool includeSummary, bool includeHighlights, int skillCap)
    {
        var lines = new List<ResumeLine>();
        var used = 0;

        bool TryAdd(string id, string text, IReadOnlyList<string> factIds)
        {
            var cost = text.Length + (lines.Count > 0 ? 1 : 0);
            if (text.Length == 0 || used + cost > budget)
            {
                return false;
            }
            lines.Add(new ResumeLine(id, text, factIds, ResumeOrigins.Generated));
            used += cost;
            return true;
        }

        string? Fact(string id) => ResumeFacts.Resolve(profile, id);

        // Identity line: title, years, industry. Dropped parts are simply omitted, never replaced.
        var title = Fact("title");
        var years = profile.YearsExperience is { } y && Fact("years") != null ? y : (int?)null;
        var industry = Fact("industry");
        var ids = new List<string>();
        var text = "";
        if (title != null)
        {
            text = title;
            ids.Add("title");
        }
        if (years != null)
        {
            text = text.Length == 0 ? $"{years} years of experience" : $"{text} with {years} years of experience";
            ids.Add("years");
        }
        if (industry != null)
        {
            text = text.Length == 0 ? industry : years != null || title != null ? $"{text} in {industry}" : text;
            ids.Add("industry");
        }
        if (!TryAdd("identity", text, ids) && title != null)
        {
            TryAdd("identity", title, new[] { "title" });
        }

        if (includeSummary && Fact("summary") is { } summary)
        {
            TryAdd("summary", summary, new[] { "summary" });
        }

        if (includeHighlights)
        {
            for (var i = 0; i < profile.Highlights.Count; i++)
            {
                if (Fact($"highlight:{i}") is { } highlight)
                {
                    TryAdd($"highlight:{i}", highlight, new[] { $"highlight:{i}" });
                }
            }
        }

        // Skills last, as many (in profile order) as still fit.
        var skillIds = new List<string>();
        var skillTexts = new List<string>();
        for (var i = 0; i < profile.Skills.Count && skillIds.Count < skillCap; i++)
        {
            if (Fact($"skill:{i}") is { } skill)
            {
                var candidate = "Skills: " + string.Join(", ", skillTexts.Append(skill));
                if (used + candidate.Length + (lines.Count > 0 ? 1 : 0) > budget)
                {
                    break;
                }
                skillIds.Add($"skill:{i}");
                skillTexts.Add(skill);
            }
        }
        if (skillIds.Count > 0)
        {
            TryAdd("skills", "Skills: " + string.Join(", ", skillTexts), skillIds);
        }
        return lines;
    }
}
