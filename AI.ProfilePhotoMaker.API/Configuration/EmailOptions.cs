namespace AI.ProfilePhotoMaker.API.Configuration;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Enable/disable transactional email sending. When false, emails are skipped but logged.
    /// </summary>
    public bool Enabled { get; set; } = false;

    public string? FromEmail { get; set; }
    public string? FromName { get; set; }

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// When true, use Brevo HTTP API instead of SMTP for sending.
    /// </summary>
    public bool UseApi { get; set; } = false;

    /// <summary>
    /// Brevo API key (xkeysib-...), used when UseApi is true.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// When true, tag emails as sandbox to avoid confusing users in lower environments.
    /// </summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>
    /// Frontend base URL for CTA links in emails (e.g., https://app.aiprofilephotomaker.com)
    /// </summary>
    public string? FrontendBaseUrl { get; set; }

    /// <summary>
    /// Internal support inbox recipient for authenticated feedback submissions.
    /// </summary>
    public string? SupportToEmail { get; set; }

    public string? SupportToName { get; set; }
}
