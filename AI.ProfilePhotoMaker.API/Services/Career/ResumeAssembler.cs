using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Builds a resume draft from a confirmed profile version (ADR 0018). Pure and deterministic: it selects and
/// orders facts by overlap with the target occupation's O*NET words and copies fact text verbatim, so it can
/// never invent a number or follow an instruction found inside a fact.
/// </summary>
public static class ResumeAssembler
{
    public const int MaxSkillLines = 20;
    public const int MaxQuestions = 10;
    private const int QuestionExcerpt = 120;

    /// <summary>The stems of an occupation's task and skill words, used to rank facts.</summary>
    public static IReadOnlySet<string> OccupationWords(OccupationRecord occupation)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in occupation.Tasks)
        {
            words.UnionWith(task.Stems);
        }
        foreach (var skill in occupation.Skills.Concat(occupation.Technologies))
        {
            words.UnionWith(OccupationText.Stems(skill));
        }
        return words;
    }

    public static ResumeDraft Assemble(CareerProfileVersion profile, string occupationTitle, IReadOnlySet<string> occupationWords)
    {
        var budget = ResumeLimits.MaxLines;
        var headline = new List<ResumeLine>();
        var summary = new List<ResumeLine>();
        var highlights = new List<ResumeLine>();
        var skills = new List<ResumeLine>();
        var questions = new List<ResumeQuestion>();

        ResumeLine Generated(string id, string text, string factId) => new(id, text, new[] { factId }, ResumeOrigins.Generated);
        bool Fits(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= ResumeLimits.MaxLineLength;

        // The headline cites the title; the target occupation is the goal's, shown as the user's aim.
        var headlineText = $"{profile.CurrentTitle} targeting {occupationTitle}";
        if (Fits(headlineText) && Fits(profile.CurrentTitle))
        {
            headline.Add(Generated("headline", headlineText, "title"));
            budget--;
        }
        if (Fits(profile.Summary) && budget > 0)
        {
            summary.Add(Generated("summary", profile.Summary!, "summary"));
            budget--;
        }

        int Overlap(string text) => OccupationText.Stems(text).Distinct().Count(occupationWords.Contains);

        var rankedHighlights = profile.Highlights
            .Select((text, index) => (text, index))
            .Where(h => Fits(h.text))
            .OrderByDescending(h => Overlap(h.text)).ThenBy(h => h.index)
            .ToList();
        foreach (var (text, index) in rankedHighlights)
        {
            if (budget <= 0)
            {
                break;
            }
            var factId = $"highlight:{index}";
            highlights.Add(Generated(factId, text, factId));
            budget--;
            // No digit means no stated result or scale; ask rather than guess one.
            if (!text.Any(char.IsDigit) && questions.Count < MaxQuestions)
            {
                var excerpt = text.Length > QuestionExcerpt ? text[..QuestionExcerpt] + "…" : text;
                questions.Add(new ResumeQuestion($"q-{factId}", factId, $"What result or scale can you add to: {excerpt}?"));
            }
        }

        var rankedSkills = profile.Skills
            .Select((text, index) => (text, index))
            .Where(s => Fits(s.text))
            .OrderByDescending(s => Overlap(s.text)).ThenBy(s => s.index)
            .Take(MaxSkillLines);
        foreach (var (text, index) in rankedSkills)
        {
            if (budget <= 0)
            {
                break;
            }
            var factId = $"skill:{index}";
            skills.Add(Generated(factId, text, factId));
            budget--;
        }

        return new ResumeDraft(
            new[]
            {
                new ResumeSection(ResumeSectionKeys.Headline, headline),
                new ResumeSection(ResumeSectionKeys.Summary, summary),
                new ResumeSection(ResumeSectionKeys.ExperienceHighlights, highlights),
                new ResumeSection(ResumeSectionKeys.Skills, skills)
            },
            questions);
    }

    /// <summary>
    /// Compares a fresh draft with the current lines. Human lines are never listed, so a proposal can never
    /// change or remove what the user wrote.
    /// </summary>
    public static IReadOnlyList<ResumeChange> Diff(IReadOnlyList<ResumeSection> current, IReadOnlyList<ResumeSection> proposed)
    {
        var changes = new List<ResumeChange>();
        var humanIds = current.SelectMany(s => s.Lines).Where(l => l.Origin == ResumeOrigins.Human).Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var key in ResumeSectionKeys.All)
        {
            var have = (current.FirstOrDefault(s => s.Key == key)?.Lines ?? Array.Empty<ResumeLine>()).ToDictionary(l => l.Id, StringComparer.Ordinal);
            var want = proposed.FirstOrDefault(s => s.Key == key)?.Lines ?? Array.Empty<ResumeLine>();
            foreach (var line in want)
            {
                if (humanIds.Contains(line.Id))
                {
                    continue;
                }
                if (!have.TryGetValue(line.Id, out var existing))
                {
                    changes.Add(new ResumeChange($"add:{line.Id}", "added", key, null, line.Text, line.FactIds));
                }
                else if (existing.Text != line.Text || !existing.FactIds.SequenceEqual(line.FactIds))
                {
                    changes.Add(new ResumeChange($"change:{line.Id}", "changed", key, existing.Text, line.Text, line.FactIds));
                }
            }
            var wantedIds = want.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var line in have.Values.Where(l => l.Origin == ResumeOrigins.Generated && !wantedIds.Contains(l.Id)))
            {
                changes.Add(new ResumeChange($"remove:{line.Id}", "removed", key, line.Text, null, line.FactIds));
            }
        }
        return changes;
    }
}
