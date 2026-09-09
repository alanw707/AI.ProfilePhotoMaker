using System.IO.Compression;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class PlatformExportServiceTests
{
    [Fact]
    public async Task Export_AllSupportedFormatsHaveAdvertisedNamesAndDimensions()
    {
        var service = new PlatformExportService();
        using var source = await CreateSourceAsync();
        var options = service.GetExportOptions();
        var bytes = await service.CreateExportPackageAsync(source, "fixture", options.Select(o => o.Code).ToArray());
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(options.Count + 1, zip.Entries.Count);
        Assert.NotNull(zip.GetEntry("README.txt"));
        foreach (var option in options)
        {
            using var entry = zip.GetEntry($"fixture-{option.FileNameSuffix}.jpg")!.Open();
            using var image = await Image.LoadAsync(entry);
            Assert.Equal(option.Width == 0 ? 100 : option.Width, image.Width);
            Assert.Equal(option.Height == 0 ? 100 : option.Height, image.Height);
        }
    }

    [Fact]
    public async Task CropOffsetChangesSquareExportAnchor()
    {
        using var source = await CreateWideSourceAsync();
        var bytes = await new PlatformExportService().CreateExportPackageAsync(source, "fixture", ["linkedin_profile"],
            new PlatformExportAdjustmentOptions { CropOffsetXPercent = -25 });
        using var leftZip = new ZipArchive(new MemoryStream(bytes));
        using var leftStream = leftZip.GetEntry("fixture-linkedin-profile.jpg")!.Open();
        using var left = await Image.LoadAsync<Rgba32>(leftStream);
        Assert.True(left[400, 400].R > 180 && left[400, 400].B < 80);

        source.Position = 0;
        bytes = await new PlatformExportService().CreateExportPackageAsync(source, "fixture", ["linkedin_profile"],
            new PlatformExportAdjustmentOptions { CropOffsetXPercent = 25 });
        using var rightZip = new ZipArchive(new MemoryStream(bytes));
        using var rightStream = rightZip.GetEntry("fixture-linkedin-profile.jpg")!.Open();
        using var right = await Image.LoadAsync<Rgba32>(rightStream);
        Assert.True(right[400, 400].B > 180 && right[400, 400].R < 80);
    }

    [Theory]
    [InlineData("linkedin_profile", "linkedin-profile", 800)]
    [InlineData("original_high_res", "original-high-res", 100)]
    public async Task ZoomChangesFramingWithoutChangingExportDimensions(string code, string suffix, int size)
    {
        using var normal = await ExportImageAsync(code, suffix, 100);
        using var zoomed = await ExportImageAsync(code, suffix, 140);
        using var zoomedOut = await ExportImageAsync(code, suffix, 80);
        Assert.Equal(size, zoomed.Width);
        Assert.Equal(size, zoomed.Height);
        Assert.Equal(size, zoomedOut.Width);
        Assert.Equal(size, zoomedOut.Height);
        // Red occupies the left 30% of the source; at 140% zoom its edge moves left.
        Assert.True(normal[size / 4, size / 2].G < 80);
        Assert.True(zoomed[size / 4, size / 2].G > 200, "Zoom must change the visible crop, not merely resample it twice.");
        Assert.True(zoomedOut[0, 0].G > 200, "Zoom-out should pad the fixed JPEG canvas with white.");
    }

    private static async Task<Image<Rgba32>> ExportImageAsync(string code, string suffix, int zoom)
    {
        using var source = await CreateSourceAsync();
        var bytes = await new PlatformExportService().CreateExportPackageAsync(source, "fixture", [code],
            new PlatformExportAdjustmentOptions { ZoomPercent = zoom });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var entry = zip.GetEntry($"fixture-{suffix}.jpg")!.Open();
        return await Image.LoadAsync<Rgba32>(entry);
    }

    private static async Task<MemoryStream> CreateWideSourceAsync()
    {
        using var image = new Image<Rgba32>(200, 100, Color.Blue);
        for (var y = 0; y < 100; y++)
        for (var x = 0; x < 100; x++)
            image[x, y] = Color.Red;
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task<MemoryStream> CreateSourceAsync()
    {
        using var image = new Image<Rgba32>(100, 100, Color.White);
        for (var y = 0; y < 100; y++)
        for (var x = 0; x < 30; x++)
            image[x, y] = new Rgba32(230, 20, 20);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }
}
