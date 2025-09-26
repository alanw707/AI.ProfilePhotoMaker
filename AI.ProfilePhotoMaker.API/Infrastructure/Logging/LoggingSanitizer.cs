using System.Text;

namespace AI.ProfilePhotoMaker.API.Infrastructure.Logging;

/// <summary>
/// Provides helpers for sanitising user supplied data before including it in logs.
/// </summary>
public static class LoggingSanitizer
{
    private const string RedactedPlaceholder = "[redacted]";
    private const int DefaultMaxLength = 256;

    /// <summary>
    /// Removes control characters and trims the value to a safe length. Returns "[redacted]" for null/whitespace.
    /// </summary>
    /// <param name="value">Input value that may originate from user input.</param>
    /// <param name="maxLength">Maximum number of characters to keep (defaults to 256).</param>
    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RedactedPlaceholder;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!char.IsControl(ch) || ch == '\t')
            {
                builder.Append(ch);
            }
        }

        if (builder.Length == 0)
        {
            return RedactedPlaceholder;
        }

        if (builder.Length > maxLength)
        {
            return builder.ToString(0, maxLength);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Convenience wrapper for sanitising identifiers (GUIDs, transaction ids, etc.).
    /// </summary>
    public static string SanitizeId(string? value) => Sanitize(value, 128);
}
