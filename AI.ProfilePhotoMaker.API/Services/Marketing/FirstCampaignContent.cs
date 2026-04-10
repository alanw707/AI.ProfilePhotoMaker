namespace AI.ProfilePhotoMaker.API.Services.Marketing;

/// <summary>
/// Provides the HTML content and metadata for the first marketing campaign:
/// "You now only need 5 selfies" — targeting no-uploads and stuck-under-minimum users.
/// </summary>
public static class FirstCampaignContent
{
    public const string Name = "5 Selfie Minimum Launch";

    public const string Subject = "Good news: you can now create AI headshots with just 5 photos";

    public const string SegmentNoUploads = SegmentFilters.NoUploads;
    public const string SegmentStuckUnderMinimum = SegmentFilters.StuckUnderMinimum;

    public const string HtmlBody = @"
<p>We heard you — getting 10+ selfies together was a real barrier.</p>
<p><strong>We've lowered the minimum to just 5 selfies.</strong></p>
<p>That's enough for our AI to learn your face and generate professional headshots in any style — LinkedIn, executive, casual, you name it.</p>
<p style=""margin:16px 0;"">Here's what you'll get:</p>
<ul style=""margin:0 0 16px; padding-left:20px; line-height:1.8;"">
  <li>AI-trained model built from your 5+ selfies</li>
  <li>Unlimited styled generations (credits permitting)</li>
  <li>LinkedIn, executive, casual, and 20+ styles</li>
  <li>Ready in minutes, not hours</li>
</ul>
<p>
  <a href=""https://aiprofilephotomaker.com/dashboard""
     style=""display:inline-block; background:#0ea5e9; color:#ffffff; text-decoration:none;
            padding:12px 24px; border-radius:8px; font-weight:600; font-size:16px;"">
    Get my headshots now
  </a>
</p>
<p style=""margin-top:16px; font-size:14px; color:#64748b;"">
  Already uploaded photos? You may already be eligible — just head to your dashboard to check.
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
