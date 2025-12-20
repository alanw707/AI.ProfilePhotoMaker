using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public static class EmailContentBuilder
{
    public static string BuildTextContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html;

        text = ReplaceAnchors(text);
        text = Regex.Replace(text, "<\\s*br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</\\s*p\\s*>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</\\s*div\\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<\\s*li\\s*>", "- ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</\\s*li\\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty, RegexOptions.Singleline);

        text = WebUtility.HtmlDecode(text);
        text = text.Replace('\u00A0', ' ');
        text = text.Replace("\r", string.Empty);
        text = Regex.Replace(text, "[ \t]+", " ");
        text = Regex.Replace(text, "\n{3,}", "\n\n");

        var lines = text.Split('\n').Select(line => line.TrimEnd());
        return string.Join("\n", lines).Trim();
    }

    private static string ReplaceAnchors(string input)
    {
        return Regex.Replace(
            input,
            "<a\\s+[^>]*href\\s*=\\s*(\"|')(?<url>[^\"']+)(\\1)[^>]*>(?<text>.*?)</a>",
            match =>
            {
                var url = WebUtility.HtmlDecode(match.Groups["url"].Value);
                var label = StripTags(match.Groups["text"].Value).Trim();

                if (string.IsNullOrWhiteSpace(label))
                {
                    return url;
                }

                if (string.Equals(label, url, StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                return $"{label}: {url}";
            },
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    private static string StripTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return Regex.Replace(input, "<[^>]+>", string.Empty, RegexOptions.Singleline);
    }
}
