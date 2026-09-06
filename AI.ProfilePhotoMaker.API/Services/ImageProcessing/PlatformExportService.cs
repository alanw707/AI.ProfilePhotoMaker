using System.IO.Compression;
using System.Numerics;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class PlatformExportService : IPlatformExportService
{
    private static readonly PlatformExportOptionDto[] Options =
    {
        new() { Code = "linkedin_profile", Label = "LinkedIn profile", Width = 800, Height = 800, FileNameSuffix = "linkedin-profile" },
        new() { Code = "linkedin_banner_safe_avatar", Label = "LinkedIn banner-safe avatar", Width = 400, Height = 400, FileNameSuffix = "linkedin-banner-safe-avatar" },
        new() { Code = "google_avatar", Label = "Gmail / Google avatar", Width = 512, Height = 512, FileNameSuffix = "google-avatar" },
        new() { Code = "slack_teams_avatar", Label = "Slack / Teams avatar", Width = 512, Height = 512, FileNameSuffix = "slack-teams-avatar" },
        new() { Code = "github_avatar", Label = "GitHub avatar", Width = 460, Height = 460, FileNameSuffix = "github-avatar" },
        new() { Code = "resume_headshot", Label = "Resume headshot", Width = 600, Height = 750, FileNameSuffix = "resume-headshot" },
        new() { Code = "realtor_square", Label = "Zillow / Realtor square", Width = 800, Height = 800, FileNameSuffix = "realtor-square" },
        new() { Code = "realtor_flyer", Label = "Realtor flyer crop", Width = 1200, Height = 1500, FileNameSuffix = "realtor-flyer" },
        new() { Code = "podcast_avatar", Label = "Podcast / press avatar", Width = 1000, Height = 1000, FileNameSuffix = "podcast-avatar" },
        new() { Code = "founder_banner", Label = "Founder LinkedIn/X banner crop", Width = 1584, Height = 396, FileNameSuffix = "founder-banner" },
        new() { Code = "website_bio", Label = "Website bio / speaker profile", Width = 1200, Height = 1200, FileNameSuffix = "website-bio" },
        new() { Code = "original_high_res", Label = "Original high-resolution image", Width = 0, Height = 0, FileNameSuffix = "original-high-res" }
    };

    public IReadOnlyList<PlatformExportOptionDto> GetExportOptions() => Options;

    public async Task<byte[]> CreateExportPackageAsync(Stream sourceImage, string baseFileName, IReadOnlyCollection<string> exportCodes, PlatformExportAdjustmentOptions? adjustments = null, CancellationToken cancellationToken = default)
    {
        var selected = ResolveSelectedOptions(exportCodes);
        using var image = await Image.LoadAsync<Rgba32>(sourceImage, cancellationToken);
        await using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var option in selected)
            {
                var entry = archive.CreateEntry($"{baseFileName}-{option.FileNameSuffix}.jpg", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();

                using var exportImage = image.Clone(ctx =>
                {
                    ApplyAdjustments(ctx, image.Size, adjustments);
                    if (option.Width > 0 && option.Height > 0)
                    {
                        ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(option.Width, option.Height),
                            Mode = ResizeMode.Crop,
                            Position = ResolveCropAnchor(adjustments)
                        });
                    }
                });

                await exportImage.SaveAsJpegAsync(entryStream, new JpegEncoder { Quality = 92 }, cancellationToken);
            }

            var readme = archive.CreateEntry("README.txt", CompressionLevel.Fastest);
            await using var readmeStream = readme.Open();
            await using var writer = new StreamWriter(readmeStream);
            await writer.WriteLineAsync("AI.ProfilePhotoMaker platform export kit");
            await writer.WriteLineAsync("Use the file whose suffix matches the platform where you plan to upload your profile photo.");
            await writer.WriteLineAsync("Exports are crops/resizes of your selected professional profile photo.");
        }

        return output.ToArray();
    }

    private static void ApplyAdjustments(IImageProcessingContext ctx, Size originalSize, PlatformExportAdjustmentOptions? adjustments)
    {
        if (adjustments == null) return;

        var brightness = Math.Clamp(adjustments.BrightnessPercent, 70, 130) / 100f;
        var contrast = Math.Clamp(adjustments.ContrastPercent, 70, 130) / 100f;
        var sharpness = Math.Clamp(adjustments.SharpnessPercent, 80, 130) / 100f;
        var rotate = Math.Clamp(adjustments.RotateDegrees, -8, 8);
        var zoom = Math.Clamp(adjustments.ZoomPercent, 80, 140) / 100f;

        if (rotate != 0 || Math.Abs(zoom - 1f) > 0.001f)
        {
            // Transform within a fixed frame so the platform resize cannot undo zoom.
            var center = new Vector2(originalSize.Width / 2f, originalSize.Height / 2f);
            var transform = Matrix3x2.CreateScale(zoom, center) *
                            Matrix3x2.CreateRotation(rotate * MathF.PI / 180f, center);
            ctx.Transform(new Rectangle(Point.Empty, originalSize), transform, originalSize, KnownResamplers.Bicubic)
                .BackgroundColor(Color.White);
        }

        if (Math.Abs(brightness - 1f) > 0.001f)
        {
            ctx.Brightness(brightness);
        }

        if (Math.Abs(contrast - 1f) > 0.001f)
        {
            ctx.Contrast(contrast);
        }

        if (Math.Abs(sharpness - 1f) > 0.001f)
        {
            ctx.GaussianSharpen(Math.Clamp(sharpness - 0.75f, 0.1f, 1.2f));
        }
    }

    private static AnchorPositionMode ResolveCropAnchor(PlatformExportAdjustmentOptions? adjustments)
    {
        if (adjustments == null) return AnchorPositionMode.Center;
        var x = Math.Clamp(adjustments.CropOffsetXPercent, -25, 25);
        var y = Math.Clamp(adjustments.CropOffsetYPercent, -25, 25);
        if (y < -8) return x < -8 ? AnchorPositionMode.TopLeft : x > 8 ? AnchorPositionMode.TopRight : AnchorPositionMode.Top;
        if (y > 8) return x < -8 ? AnchorPositionMode.BottomLeft : x > 8 ? AnchorPositionMode.BottomRight : AnchorPositionMode.Bottom;
        return x < -8 ? AnchorPositionMode.Left : x > 8 ? AnchorPositionMode.Right : AnchorPositionMode.Center;
    }

    private static IReadOnlyList<PlatformExportOptionDto> ResolveSelectedOptions(IReadOnlyCollection<string> exportCodes)
    {
        if (exportCodes.Count == 0)
        {
            return Options;
        }

        var selected = Options
            .Where(o => exportCodes.Contains(o.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return selected.Count > 0 ? selected : Options;
    }
}
