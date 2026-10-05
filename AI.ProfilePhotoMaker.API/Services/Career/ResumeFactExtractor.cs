using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>One proposed fact with where it came from.</summary>
public sealed record ExtractedItem(
    string Field,
    string Value,
    int? Page,
    string? Section,
    string Excerpt,
    IReadOnlyList<string> Flags);

/// <summary>
/// Rule-based, deterministic extraction of profile facts from resume text. The text
/// is data only: every rule is a pattern match that produces a proposed value, and
/// nothing in the text can change behaviour or be applied without user acceptance.
/// Names are never extracted.
/// </summary>
public static partial class ResumeFactExtractor
{
    public const int MaxExcerpt = 300;
    private const int HeaderLines = 12;

    private static readonly string[] TitleWords =
    {
        "lead", "manager", "analyst", "engineer", "director", "specialist", "coordinator", "developer",
        "designer", "administrator", "consultant", "officer", "nurse", "teacher", "accountant",
        "associate", "supervisor", "technician", "architect", "scientist", "assistant", "representative"
    };

    private static readonly string[] VagueClaims =
    {
        "helped", "involved in", "assisted", "responsible for", "worked on", "participated in",
        "contributed to", "various", "exposure to"
    };

    private static readonly string[] Months =
        { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

    private enum SectionKind { Header, Summary, Experience, Skills, Other }

    private sealed record Line(string Text, int Page, string Section, SectionKind Kind);

    private sealed record DateRange(int Start, int End, bool Contradictory, Line Source);

    /// <param name="pages">Text per page, lines separated by newlines.</param>
    /// <param name="pasted">Pasted text has no page numbers.</param>
    /// <param name="asOf">What "Present" means, so results do not depend on the wall clock.</param>
    public static IReadOnlyList<ExtractedItem> Extract(IReadOnlyList<string> pages, bool pasted, DateTime asOf)
    {
        var lines = ReadLines(pages);
        int? PageOf(Line line) => pasted ? null : line.Page;
        var items = new List<ExtractedItem>();

        ExtractedItem Item(string field, string value, Line line, params string[] flags) =>
            new(field, value, PageOf(line), line.Section, Excerpt(line.Text), flags);

        // Title: a title-cased line ending in a role word, above the first section;
        // otherwise the role of a job that is still current (older jobs are not "current title").
        var titleLine = lines.Where(l => l.Kind == SectionKind.Header).Take(HeaderLines).FirstOrDefault(l => LooksLikeTitle(l.Text))
            ?? lines.Where(l => l.Kind == SectionKind.Experience)
                .FirstOrDefault(l => LooksLikeTitle(FirstSegment(l.Text)) && CurrentJobPattern().IsMatch(l.Text));
        if (titleLine != null)
        {
            var title = titleLine.Kind == SectionKind.Header ? titleLine.Text : FirstSegment(titleLine.Text);
            items.Add(Item("currentTitle", title, titleLine));
        }

        // Location: a whole "City, ST" segment near the top.
        foreach (var line in lines.Where(l => l.Kind == SectionKind.Header).Take(HeaderLines))
        {
            var segment = line.Text.Split('|', '•', '·').Select(s => s.Trim()).FirstOrDefault(s => LocationPattern().IsMatch(s));
            if (segment != null)
            {
                items.Add(Item("location", segment, line));
                break;
            }
        }

        var summary = lines.Where(l => l.Kind == SectionKind.Summary).ToList();
        if (summary.Count > 0)
        {
            var text = string.Join(" ", summary.Select(l => l.Text));
            items.Add(Item("summary", Truncate(text, CareerInputValidator.MaxSummary), summary[0]));
        }

        AddYears(lines, asOf, items, Item);

        var seenSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Where(l => l.Kind == SectionKind.Skills))
        {
            foreach (var skill in SplitSkills(StripBullet(line.Text)))
            {
                if (items.Count(i => i.Field == "skills") < CareerInputValidator.MaxSkills && seenSkills.Add(skill))
                {
                    items.Add(Item("skills", skill, line));
                }
            }
        }

        foreach (var line in lines.Where(l => l.Kind == SectionKind.Experience && IsBullet(l.Text)))
        {
            var bullet = StripBullet(line.Text);
            if (bullet.Length < 15 || items.Count(i => i.Field == "highlights") >= CareerInputValidator.MaxHighlights)
            {
                continue;
            }

            var flags = IsVague(bullet) ? new[] { ProposalFlags.Ambiguous } : Array.Empty<string>();
            items.Add(Item("highlights", Truncate(bullet, CareerInputValidator.MaxHighlightLength), line, flags));
        }

        return items;
    }

    // ---- Years of experience ---------------------------------------------

    private static void AddYears(
        List<Line> lines, DateTime asOf, List<ExtractedItem> items, Func<string, string, Line, string[], ExtractedItem> item)
    {
        var now = asOf.Year * 12 + asOf.Month - 1;
        var ranges = new List<DateRange>();
        foreach (var line in lines.Where(l => l.Kind == SectionKind.Experience))
        {
            var match = DateRangePattern().Match(line.Text);
            if (!match.Success)
            {
                continue;
            }

            var start = ToMonthIndex(match.Groups["s"].Value, endOfPeriod: false, now);
            var end = ToMonthIndex(match.Groups["e"].Value, endOfPeriod: true, now);
            if (start is { } s && end is { } e)
            {
                ranges.Add(new DateRange(s, e, Contradictory: e < s, line));
            }
        }

        var usable = ranges.Where(r => !r.Contradictory).OrderBy(r => r.Start).ToList();
        if (usable.Count == 0)
        {
            return;
        }

        // Merge overlapping ranges so concurrent jobs are not double counted.
        long months = 0;
        var currentStart = usable[0].Start;
        var currentEnd = usable[0].End;
        foreach (var range in usable.Skip(1))
        {
            if (range.Start <= currentEnd + 1)
            {
                currentEnd = Math.Max(currentEnd, range.End);
            }
            else
            {
                months += currentEnd - currentStart + 1;
                (currentStart, currentEnd) = (range.Start, range.End);
            }
        }
        months += currentEnd - currentStart + 1;

        var years = (int)(months / 12);
        if (years is < 0 or > 60)
        {
            return;
        }

        // Overlap beyond a few months of handover, or an end before its start, disagrees with itself.
        var conflict = ranges.Any(r => r.Contradictory) || usable.Zip(usable.Skip(1)).Any(p => p.First.End - p.Second.Start >= 3);
        var flags = conflict ? new[] { ProposalFlags.Conflict } : Array.Empty<string>();
        items.Add(item("yearsExperience", years.ToString(CultureInfo.InvariantCulture), usable[0].Source, flags));
    }

    private static int? ToMonthIndex(string text, bool endOfPeriod, int now)
    {
        text = text.Trim();
        if (text.Equals("present", StringComparison.OrdinalIgnoreCase) || text.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            return now;
        }

        var year = YearPattern().Match(text);
        if (!year.Success)
        {
            return null;
        }

        var monthName = text.Length >= 3 ? text[..3].ToLowerInvariant() : string.Empty;
        var month = Array.IndexOf(Months, monthName);
        if (month < 0)
        {
            month = endOfPeriod ? 11 : 0;
        }
        return int.Parse(year.Value, CultureInfo.InvariantCulture) * 12 + month;
    }

    // ---- Lines and sections ----------------------------------------------

    private static List<Line> ReadLines(IReadOnlyList<string> pages)
    {
        var result = new List<Line>();
        var kind = SectionKind.Header;
        var section = "Header";

        for (var p = 0; p < pages.Count; p++)
        {
            foreach (var raw in pages[p].Split('\n'))
            {
                var text = Clean(raw);
                if (text.Length == 0)
                {
                    continue;
                }

                var heading = text.TrimEnd(':').Trim();
                if (HeadingKind(heading) is { } headingKind)
                {
                    kind = headingKind;
                    section = heading;
                    continue;
                }
                result.Add(new Line(text, p + 1, section, kind));
            }
        }
        return result;
    }

    private static SectionKind? HeadingKind(string heading)
    {
        var normal = heading.ToLowerInvariant();
        return normal switch
        {
            "summary" or "profile" or "professional summary" or "objective" => SectionKind.Summary,
            "experience" or "work experience" or "professional experience" or "employment history" or "employment" => SectionKind.Experience,
            "skills" or "technical skills" or "core skills" or "key skills" or "core competencies" => SectionKind.Skills,
            "education" or "certifications" or "projects" or "awards" or "references" or "publications"
                or "volunteer experience" or "languages" or "interests" => SectionKind.Other,
            _ => null
        };
    }

    /// <summary>Removes control characters so excerpts and values are plain text.</summary>
    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }
        return WhitespacePattern().Replace(builder.ToString(), " ").Trim();
    }

    // ---- Rules -------------------------------------------------------------

    private static bool LooksLikeTitle(string text)
    {
        if (text.Length is 0 or > 60 || text.IndexOfAny(new[] { ',', '.', ':', ';', '!', '?', '@', '/', '(', ')' }) >= 0)
        {
            return false;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 6 || !TitleWords.Contains(words[^1].ToLowerInvariant()))
        {
            return false;
        }

        // Title Case only, so a lowercase sentence is never mistaken for a title.
        return words.All(w => char.IsUpper(w[0]) || w is "of" or "and" or "&" or "the");
    }

    private static string FirstSegment(string text) => text.Split(',')[0].Trim();

    private static bool IsBullet(string text) => text.Length > 1 && "-•*·".Contains(text[0]) && (text[1] == ' ' || text[0] == '•');

    private static string StripBullet(string text) => IsBullet(text) ? text[1..].Trim() : text;

    private static bool IsVague(string bullet) =>
        VagueClaims.Any(v => bullet.Contains(v, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitSkills(string text) =>
        text.Split(',', ';', '•', '|', '·')
            .Select(s => s.Trim())
            .Where(s => s.Length is > 0 and <= CareerInputValidator.MaxSkillLength);

    private static string Excerpt(string text) => Truncate(text, MaxExcerpt);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max].TrimEnd();

    [GeneratedRegex(@"^[A-Z][A-Za-z.' \-]{1,40}, [A-Z]{2}(?: \d{5})?$")]
    private static partial Regex LocationPattern();

    [GeneratedRegex(@"(?<s>(?:[A-Za-z]{3,9}\.?\s+)?(?:19|20)\d{2})\s*(?:-|–|—|to)\s*(?<e>(?:[A-Za-z]{3,9}\.?\s+)?(?:19|20)\d{2}|Present|Current)", RegexOptions.IgnoreCase)]
    private static partial Regex DateRangePattern();

    [GeneratedRegex(@"(?:-|–|—|to)\s*(?:present|current)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentJobPattern();

    [GeneratedRegex(@"(?:19|20)\d{2}")]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
