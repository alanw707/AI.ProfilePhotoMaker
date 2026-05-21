namespace AI.ProfilePhotoMaker.API.Services.Marketing;

/// <summary>
/// Provides the HTML content and metadata for the first marketing campaign:
/// "You can now start from one clear photo" — targeting no-uploads and stuck-under-minimum users.
/// </summary>
public static class FirstCampaignContent
{
    public const string Name = "5 Selfie Minimum Launch";

    public const string Subject = "Good news: you can now create AI headshots from one clear photo";

    public const string SegmentNoUploads = SegmentFilters.NoUploads;
    public const string SegmentStuckUnderMinimum = SegmentFilters.StuckUnderMinimum;

    public const string HtmlBody = @"
<p>We heard you — getting one clear photo together was a real barrier.</p>
<p><strong>We've lowered the minimum to one clear photo.</strong></p>
<p>That is enough to create an instant headshot. Advanced custom photoshoot packs remain available when you want more variety.</p>
<p style=""margin:16px 0;"">Here's what you'll get:</p>
<ul style=""margin:0 0 16px; padding-left:20px; line-height:1.8;"">
  <li>AI-trained model once you reach the 5-photo minimum</li>
  <li>Unlimited styled generations (credits permitting)</li>
  <li>LinkedIn, executive, casual, and 20+ styles</li>
  <li>Ready in minutes, not hours</li>
</ul>
<p>
  <a href=""https://aiprofilephotomaker.com/app/enhance""
     style=""display:inline-block; background:#0ea5e9; color:#ffffff; text-decoration:none;
            padding:12px 24px; border-radius:8px; font-weight:600; font-size:16px;"">
    Get my headshots now
  </a>
</p>
<p style=""margin-top:16px; font-size:14px; color:#64748b;"">
  Already uploaded photos? You may already be eligible — just head to your Photo Workspace to check.
</p>";

    /// <summary>
    /// Returns the CreateCampaignRequest payload for this campaign, ready to POST.
    /// Pass the desired segment filter (no-uploads or stuck-under-minimum).
    /// </summary>
    public static object BuildRequest(string segmentFilter, DateTime? scheduledAt = null) => new
    {
        name = segmentFilter == SegmentFilters.NoUploads
            ? $"{Name} — Never Uploaded"
            : $"{Name} — 1-4 Uploads",
        subject = Subject,
        htmlBody = HtmlBody,
        segmentFilter,
        scheduledAt
    };
}
