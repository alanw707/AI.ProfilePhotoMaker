using System.Collections.Concurrent;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Keeps stored bytes so tests can see exactly what was stored and deleted.</summary>
public sealed class InMemoryStorage : IStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public IReadOnlyCollection<string> Keys => _files.Keys.ToArray();
    public int Count => _files.Count;
    public bool Contains(string key) => _files.ContainsKey(key);

    public Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
    {
        using var copy = new MemoryStream();
        imageStream.CopyTo(copy);
        _files[storagePath] = copy.ToArray();
        return Task.FromResult(storagePath);
    }

    public Task<Stream?> GetImageAsync(string storagePath) =>
        Task.FromResult<Stream?>(_files.TryGetValue(storagePath, out var bytes) ? new MemoryStream(bytes) : null);

    public Task<bool> DeleteImageAsync(string storagePath) => Task.FromResult(_files.TryRemove(storagePath, out _));
    public Task<bool> ExistsAsync(string storagePath) => Task.FromResult(_files.ContainsKey(storagePath));

    public Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated") =>
        SaveImageToPathAsync(imageStream, $"{userId}/{folderType}/{fileName}");

    public string GetImageUrl(string storagePath) => storagePath;
    public Task<List<string>> ListUserImagesAsync(string userId) => Task.FromResult(new List<string>());
    public Task<StorageFileInfo?> GetFileInfoAsync(string storagePath) => Task.FromResult<StorageFileInfo?>(null);
    public Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, Azure.Storage.Sas.BlobSasPermissions permissions = Azure.Storage.Sas.BlobSasPermissions.Read) =>
        Task.FromResult(storagePath);
    public Task<string> SaveZipAsync(Stream zipStream, string storagePath) => SaveImageToPathAsync(zipStream, storagePath);
    public Task<bool> DeleteDirectoryAsync(string directoryPath) => Task.FromResult(true);
    public Task<List<string>> ListFilesAsync(string prefix) => Task.FromResult(new List<string>());
}

public sealed class ControllableScanner : IMalwareScanner
{
    public MalwareScanResult Result { get; set; } = MalwareScanResult.Clean;
    public Exception? Throw { get; set; }
    public int Calls { get; private set; }

    public Task<MalwareScanResult> ScanAsync(byte[] content, CancellationToken ct = default)
    {
        Calls++;
        if (Throw != null)
        {
            throw Throw;
        }
        return Task.FromResult(Result);
    }
}

/// <summary>Never finishes on its own, so the extraction timeout must fire.</summary>
public sealed class SlowParser : IResumeParser
{
    public async Task<ResumeParseResult> ParseAsync(byte[] content, ResumeFormat format, CancellationToken ct = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        return new ResumeParseResult(1, new[] { "never" });
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            owner.Messages.Enqueue(formatter(state, exception) + " " + exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    owner.Messages.Enqueue($"{pair.Key}={pair.Value}");
                }
            }
        }
    }
}

/// <summary>
/// Career-enabled host with an in-memory private store and a controllable scanner.
/// Set the init properties before the first request.
/// </summary>
public sealed class ResumeImportFactory : CareerWorkspaceEnabledFactory
{
    public InMemoryStorage Storage { get; } = new();
    public ControllableScanner Scanner { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();
    public bool RegisterScanner { get; init; } = true;
    public IResumeParser? Parser { get; init; }
    public double? ExtractionTimeoutSeconds { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        if (ExtractionTimeoutSeconds is { } seconds)
        {
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Career:ExtractionTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStorageService>(Storage);
            services.AddSingleton<ILoggerProvider>(Logs);
            if (RegisterScanner)
            {
                services.AddSingleton<IMalwareScanner>(Scanner);
            }
            if (Parser != null)
            {
                services.AddSingleton(Parser);
            }
        });
    }
}
