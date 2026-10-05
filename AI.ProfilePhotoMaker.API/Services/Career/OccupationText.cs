using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Tokenising shared by the snapshot index and the matcher, so a duty and a task are
/// always reduced the same way: lowercase, stop words dropped, light suffix stemming.
/// </summary>
public static class OccupationText
{
    private static readonly Regex TokenPattern = new(@"[a-z0-9]+(?:[#+]+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SkillNoise = new(@"[^a-z0-9#+. ]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "about", "above", "across", "after", "all", "also", "an", "and", "any", "are", "as", "at", "be", "been",
        "between", "both", "but", "by", "can", "did", "do", "does", "during", "each", "etc", "for", "from", "had",
        "has", "have", "he", "her", "his", "how", "i", "if", "in", "including", "into", "is", "it", "its", "may",
        "more", "most", "my", "of", "on", "or", "other", "our", "out", "over", "per", "related", "such", "that",
        "the", "their", "them", "then", "there", "these", "they", "this", "through", "to", "under", "up", "using",
        "was", "we", "were", "what", "when", "which", "while", "who", "will", "with", "within", "work", "worked",
        "working", "you", "your", "various", "multiple", "new", "using", "used", "use", "ensure", "ensured", "provide",
        "provided", "perform", "performed", "responsible", "team", "teams", "daily", "day", "variety", "like", "well"
    };

    /// <summary>Content stems of the text in order, stop words removed.</summary>
    public static IReadOnlyList<string> Stems(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var stems = new List<string>();
        foreach (Match match in TokenPattern.Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length < 2 || StopWords.Contains(token))
            {
                continue;
            }
            var stem = Stem(token);
            if (stem.Length >= 2 && !StopWords.Contains(stem))
            {
                stems.Add(stem);
            }
        }
        return stems;
    }

    /// <summary>Lowercase, punctuation-light form used to compare skill names for equality.</summary>
    public static string NormalizeSkill(string? name)
    {
        var lowered = SkillNoise.Replace((name ?? string.Empty).ToLowerInvariant(), " ");
        return string.Join(' ', lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.');
    }

    // Deliberately light: enough that "developed", "developing" and "developers" meet
    // "develop", not a full stemmer. Both sides use the same rules, so odd stems still match.
    private static string Stem(string token)
    {
        if (token.Length <= 3 || token.Any(char.IsDigit) || token.EndsWith('#') || token.EndsWith('+'))
        {
            return token;
        }

        var s = token;
        if (s.EndsWith("ies", StringComparison.Ordinal) && s.Length > 4)
        {
            s = s[..^3] + "y";
        }
        else if (s.EndsWith("sses", StringComparison.Ordinal))
        {
            s = s[..^2];
        }
        else if (s.EndsWith('s') && !s.EndsWith("ss", StringComparison.Ordinal) && !s.EndsWith("us", StringComparison.Ordinal)
            && !s.EndsWith("is", StringComparison.Ordinal))
        {
            s = s[..^1];
        }

        foreach (var suffix in new[] { "ing", "ed", "er", "or", "ly", "ion", "ment" })
        {
            if (s.EndsWith(suffix, StringComparison.Ordinal) && s.Length - suffix.Length >= 4)
            {
                s = s[..^suffix.Length];
                // "planning" -> "plann" -> "plan".
                if (s.Length > 3 && s[^1] == s[^2] && !"lsz".Contains(s[^1]))
                {
                    s = s[..^1];
                }
                break;
            }
        }

        if (s.EndsWith('e') && s.Length > 4)
        {
            s = s[..^1];
        }
        if (s.EndsWith("at", StringComparison.Ordinal) && s.Length > 5)
        {
            // "evaluate"/"evaluation" both meet at "evalu".
            s = s[..^2];
        }
        return s;
    }
}
