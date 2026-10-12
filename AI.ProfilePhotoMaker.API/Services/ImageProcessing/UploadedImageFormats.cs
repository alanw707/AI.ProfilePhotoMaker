using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// The image formats a user may upload: JPEG, PNG and WebP. Uploads are checked by content, not file
/// name, and decoded with only these decoders so other formats (TIFF, BMP, GIF...) never reach a parser.
/// </summary>
public static class UploadedImageFormats
{
    private const int HeaderLength = 12;

    /// <summary>Decoder options limited to JPEG, PNG and WebP.</summary>
    public static DecoderOptions DecoderOptions { get; } = new()
    {
        Configuration = new SixLabors.ImageSharp.Configuration(new JpegConfigurationModule(), new PngConfigurationModule(), new WebpConfigurationModule())
    };

    /// <summary>True when the bytes start like a JPEG, PNG or WebP file.</summary>
    public static bool IsSupported(ReadOnlySpan<byte> header) =>
        (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        || (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        || (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8));

    /// <summary>Checks the start of a seekable stream and rewinds it.</summary>
    public static async Task<bool> IsSupportedAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[HeaderLength];
        var start = stream.Position;
        var read = await stream.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, ct);
        stream.Position = start;
        return IsSupported(header.AsSpan(0, read));
    }
}
