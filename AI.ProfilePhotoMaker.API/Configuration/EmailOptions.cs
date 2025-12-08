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
    /// When true, tag emails as sandbox to avoid confusing users in lower environments.
    /// </summary>
    public bool SandboxMode { get; set; } = true;
}
