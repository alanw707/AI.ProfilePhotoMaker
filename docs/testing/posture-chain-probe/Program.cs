using System.Security.Cryptography;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length == 2 && args[0] == "create")
{
    Directory.CreateDirectory(args[1]);
    foreach (var name in new[] { "source", "response" })
    {
        using var image = new Image<Rgba32>(128, 128);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var alternate = (x / 16 + y / 16) % 2 == 0;
            image[x, y] = name == "source"
                ? alternate ? new Rgba32(190, 35, 55) : new Rgba32(30, 65, 170)
                : alternate ? new Rgba32(25, 150, 85) : new Rgba32(220, 170, 30);
        }
        await image.SaveAsPngAsync(Path.Combine(args[1], name + ".png"));
    }
    Console.WriteLine("Created distinct deterministic PNG fixtures; not AI output.");
    return;
}

if (args.Length != 5 || args[0] != "verify")
    throw new ArgumentException("Use create <directory> or verify <selected> <provider-input> <provider-output> <saved-result>.");

string PixelHash(string path)
{
    using var image = Image.Load<Rgba32>(path);
    // A small square fixture needs neither padding nor resizing in the provider adapter.
    if (image.Width != 128 || image.Height != 128)
        throw new InvalidOperationException("Unexpected fixture dimensions.");
    var pixels = new byte[image.Width * image.Height * 4];
    image.CopyPixelDataTo(pixels);
    return Convert.ToHexString(SHA256.HashData(pixels));
}

var selected = PixelHash(args[1]);
var providerInput = PixelHash(args[2]);
var providerOutput = PixelHash(args[3]);
var saved = PixelHash(args[4]);
if (selected != providerInput || providerOutput != saved || selected == saved)
    throw new InvalidOperationException("Selected/provider/saved pixel identity mismatch.");
Console.WriteLine(JsonSerializer.Serialize(new {
    selectedPixelHash = selected, providerInputPixelHash = providerInput,
    providerOutputPixelHash = providerOutput, savedPixelHash = saved,
    selectedReachedProvider = true, distinctProviderOutputSaved = true
}));
