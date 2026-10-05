namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// The only facts the matcher may see (ADR 0010). Location, work arrangement, pay, goal
/// and contact details are deliberately not here, so they cannot change a result.
/// </summary>
public sealed record OccupationMatchInput(
    string CurrentTitle,
    string? Industry,
    string? Summary,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Highlights);

public sealed record OccupationEvidence(
    string Kind,
    string ProfileField,
    int ProfileIndex,
    string ProfileText,
    string ReferenceKind,
    string? ReferenceId,
    string ReferenceText);

public sealed record OccupationCandidate(
    string Code,
    string Title,
    string Description,
    string Strength,
    IReadOnlyList<OccupationEvidence> Evidence,
    bool TitleMatched,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> KnownGaps,
    IReadOnlyList<string> UnsupportedSkills);

public sealed record OccupationMatchResult(
    string Status,
    bool Ambiguous,
    IReadOnlyList<OccupationCandidate> Candidates,
    string? Guidance,
    string MatcherVersion);

/// <summary>
/// Deterministic duty-overlap matching against the pinned O*NET snapshot (ADR 0010). The same
/// input and snapshot always give the same result; nothing here calls a model or the network.
/// </summary>
public static class OccupationMatcher
{
    public const string Version = "duty-overlap-1";
    public const string Candidates = "candidates";
    public const string Unsupported = "unsupported";

    public const string UnsupportedGuidance =
        "Describe the work you do in your profile highlights, such as the responsibilities you handle day to day. "
        + "We suggest occupations from your responsibilities, not from a job title alone.";

    // A duty supports a task when they share at least this many content stems...
    internal const int MinSharedStems = 2;
    // ...those shared stems carry at least this much IDF weight (rare words count more)...
    internal const double MinSharedWeight = 8.0;
    // ...and cover at least this share of the duty's own weight, so one rare word in a long duty is not enough.
    internal const double MinDutyCoverage = 0.35;

    internal const double SkillWeight = 3.0;
    internal const double TitleBonus = 3.0;

    internal const int MaxCandidates = 5;
    internal const int MaxListed = 5;

    // Strength comes from how many duties and how much weight support the occupation.
    internal const int StrongDuties = 3;
    internal const double StrongWeight = 40.0;
    internal const int ModerateDuties = 2;

    // Ambiguous: the runner-up is in another SOC major group and nearly as well supported.
    internal const double AmbiguityRatio = 0.8;

    private const int MaxSummarySentences = 8;

    public static OccupationMatchResult Match(OccupationReferenceData reference, OccupationMatchInput input)
    {
        var duties = CollectDuties(input);
        var skills = input.Skills.Select((text, index) => (Text: text, Index: index, Key: OccupationText.NormalizeSkill(text))).ToList();
        var titleKey = string.Join(' ', OccupationText.Stems(input.CurrentTitle));

        var scored = new List<Scored>();
        foreach (var occupation in reference.Occupations)
        {
            var candidate = Score(reference, occupation, duties, skills, titleKey);
            if (candidate != null)
            {
                scored.Add(candidate);
            }
        }

        // Weight first, then code, so ties never depend on snapshot order.
        var ranked = scored.OrderByDescending(s => s.Weight).ThenBy(s => s.Occupation.Code, StringComparer.Ordinal).Take(MaxCandidates).ToList();
        if (ranked.Count == 0)
        {
            return new OccupationMatchResult(Unsupported, false, Array.Empty<OccupationCandidate>(), UnsupportedGuidance, Version);
        }

        var ambiguous = ranked.Count > 1
            && ranked[0].Occupation.MajorGroup != ranked[1].Occupation.MajorGroup
            && ranked[1].Weight >= AmbiguityRatio * ranked[0].Weight;
        return new OccupationMatchResult(
            Candidates, ambiguous, ranked.Select(s => s.Candidate).ToList(), null, Version);
    }

    private sealed record Duty(string Field, int Index, string Text, HashSet<string> Stems, double Weight);

    private sealed record Scored(OccupationRecord Occupation, double Weight, OccupationCandidate Candidate);

    private static List<Duty> CollectDuties(OccupationMatchInput input)
    {
        var duties = new List<Duty>();
        for (var i = 0; i < input.Highlights.Count; i++)
        {
            AddDuty(duties, "highlights", i, input.Highlights[i]);
        }

        // Summary sentences are duties too, but only a few: a summary is mostly self-description.
        var sentences = (input.Summary ?? string.Empty).Split(new[] { '.', '!', '?', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < sentences.Length && i < MaxSummarySentences; i++)
        {
            AddDuty(duties, "summary", i, sentences[i].Trim());
        }
        return duties;

        void AddDuty(List<Duty> list, string field, int index, string text)
        {
            var stems = OccupationText.Stems(text).ToHashSet(StringComparer.Ordinal);
            if (stems.Count >= MinSharedStems)
            {
                list.Add(new Duty(field, index, text, stems, 0));
            }
        }
    }

    private static Scored? Score(
        OccupationReferenceData reference, OccupationRecord occupation, List<Duty> duties,
        List<(string Text, int Index, string Key)> skills, string titleKey)
    {
        var evidence = new List<OccupationEvidence>();
        var covered = new HashSet<int>();
        var weight = 0.0;

        foreach (var duty in duties)
        {
            var dutyWeight = duty.Stems.Sum(reference.IdfOf);
            OccupationTask? best = null;
            var bestShared = 0.0;
            foreach (var task in occupation.Tasks)
            {
                var shared = 0.0;
                var count = 0;
                foreach (var stem in task.Stems.Distinct())
                {
                    if (duty.Stems.Contains(stem))
                    {
                        count++;
                        shared += reference.IdfOf(stem);
                    }
                }
                if (count >= MinSharedStems && shared >= MinSharedWeight && shared >= MinDutyCoverage * dutyWeight && shared > bestShared)
                {
                    best = task;
                    bestShared = shared;
                }
            }

            if (best != null)
            {
                weight += bestShared;
                covered.Add(best.Id);
                evidence.Add(new OccupationEvidence("duty", duty.Field, duty.Index, duty.Text, "task", best.Id.ToString(), best.Text));
            }
        }

        var dutyCount = evidence.Count;
        var unsupportedSkills = new List<string>();
        var supportedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            if (skill.Key.Length > 0 && occupation.SkillIndex.TryGetValue(skill.Key, out var reference_) && supportedKeys.Add(skill.Key))
            {
                weight += SkillWeight;
                evidence.Add(new OccupationEvidence("skill", "skills", skill.Index, skill.Text, reference_.Kind, null, reference_.Text));
            }
            else if (!string.IsNullOrWhiteSpace(skill.Text))
            {
                unsupportedSkills.Add(skill.Text);
            }
        }

        // One duty plus one more piece of evidence at least; a title never counts, and never starts a candidate.
        if (dutyCount < 1 || evidence.Count < 2)
        {
            return null;
        }

        var titleMatched = titleKey.Length > 0 && occupation.TitleKeys.Contains(titleKey);
        if (titleMatched)
        {
            weight += TitleBonus;
        }

        var missing = occupation.Tasks.Where(t => !covered.Contains(t.Id)).Select(t => t.Text).Take(MaxListed).ToList();
        var gaps = occupation.Skills.Concat(occupation.Technologies)
            .Where(name => !supportedKeys.Contains(OccupationText.NormalizeSkill(name)))
            .Take(MaxListed)
            .ToList();

        var strength = dutyCount >= StrongDuties && weight >= StrongWeight ? "strong"
            : dutyCount >= ModerateDuties ? "moderate"
            : "weak";

        var candidate = new OccupationCandidate(
            occupation.Code, occupation.Title, occupation.Description, strength, evidence, titleMatched, missing, gaps, unsupportedSkills);
        return new Scored(occupation, weight, candidate);
    }
}
