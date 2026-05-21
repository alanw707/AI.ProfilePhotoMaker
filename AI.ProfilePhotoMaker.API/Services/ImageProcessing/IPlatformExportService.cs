using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public interface IPlatformExportService
{
    IReadOnlyList<PlatformExportOptionDto> GetExportOptions();
    Task<byte[]> CreateExportPackageAsync(Stream sourceImage, string baseFileName, IReadOnlyCollection<string> exportCodes, PlatformExportAdjustmentOptions? adjustments = null, CancellationToken cancellationToken = default);
}

public class PlatformExportAdjustmentOptions
{
    public int ZoomPercent { get; set; } = 100;
    public int RotateDegrees { get; set; } = 0;
    public int BrightnessPercent { get; set; } = 100;
    public int ContrastPercent { get; set; } = 100;
    public int SharpnessPercent { get; set; } = 100;
    public int CropOffsetXPercent { get; set; }
    public int CropOffsetYPercent { get; set; }
}
