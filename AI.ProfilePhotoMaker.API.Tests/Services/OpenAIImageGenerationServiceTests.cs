using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services
{
    public class OpenAIImageGenerationServiceTests
    {
        [Fact]
        public async Task EnhancePhotoQualityAsync_SendsConfiguredModelParameter()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler();
            var httpClient = new HttpClient(handler);

            var configDict = new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key",
                ["OpenAI:ImageModel"] = "gpt-image-2"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            var logger = NullLogger<OpenAIImageGenerationService>.Instance;
            var storage = new Mock<IStorageService>(MockBehavior.Strict); // not used by this method

            // Provide a factory that returns the same mocked HttpClient for both OpenAI and download paths
            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = new OpenAIImageGenerationService(httpClient, factory.Object, configuration, logger, storage.Object);

            // Create a valid 2x2 PNG as source image
            byte[] inputPngBytes;
            using (var img = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0, 255)))
            using (var ms = new MemoryStream())
            {
                img.Save(ms, new PngEncoder());
                inputPngBytes = ms.ToArray();
            }
            handler.InputImageBytes = inputPngBytes;

            var request = new EnhancePhotoRequestDto
            {
                ImageUrl = "https://example.com/test.png",
                EnhancementType = "chibi"
            };

            // Act
            var dataUrl = await service.EnhancePhotoQualityAsync(request);

            // Assert: verify POST to images/edits contains the configured model
            handler.LastEditPostContent.Should().NotBeNull();
            var postBody = handler.LastEditPostContent!;
            AssertMultipartField(postBody, "model");
            postBody.Should().Contain("gpt-image-2");
            AssertMultipartField(postBody, "image");
            AssertNoMultipartField(postBody, "image[]");
            AssertMultipartField(postBody, "prompt");
            AssertMultipartField(postBody, "size");

            // Assert: result is data URL
            dataUrl.Should().StartWith("data:image/png;base64,");
        }

        [Fact]
        public async Task EnhancePhotoQualityAsync_WithStoragePath_LoadsFromStorageAndDoesNotDownloadSourceUrl()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler();
            var httpClient = new HttpClient(handler);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "test-key",
                    ["OpenAI:ImageModel"] = "gpt-image-2"
                })
                .Build();

            byte[] inputPngBytes;
            using (var img = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0, 255)))
            using (var ms = new MemoryStream())
            {
                img.Save(ms, new PngEncoder());
                inputPngBytes = ms.ToArray();
            }

            handler.InputImageBytes = inputPngBytes;

            var storagePath = "testing/enhanced/test-user-1/source.png";
            var storage = new Mock<IStorageService>(MockBehavior.Strict);
            storage.Setup(s => s.GetImageAsync(storagePath))
                .ReturnsAsync(new MemoryStream(inputPngBytes));

            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = new OpenAIImageGenerationService(
                httpClient,
                factory.Object,
                configuration,
                NullLogger<OpenAIImageGenerationService>.Instance,
                storage.Object);

            var request = new EnhancePhotoRequestDto
            {
                ImageUrl = "https://example.com/should-not-be-fetched.png",
                ImageStoragePath = storagePath,
                EnhancementType = "headshot"
            };

            // Act
            var dataUrl = await service.EnhancePhotoQualityAsync(request);

            // Assert
            dataUrl.Should().StartWith("data:image/png;base64,");
            handler.SourceImageGetCount.Should().Be(0);
            storage.Verify(s => s.GetImageAsync(storagePath), Times.Once);

            var postBody = handler.LastEditPostContent!;
            postBody.Should().Contain("gpt-image-2");
            AssertMultipartField(postBody, "image");
            AssertNoMultipartField(postBody, "image[]");
            postBody.Should().Contain("natural professional headshot");
            postBody.Should().Contain("realistic skin texture");
            postBody.Should().Contain("waxy smoothing");
        }

        [Fact]
        public async Task LiveOpenAiSmoke_WhenExplicitlyEnabled_GeneratesHeadshot()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("RUN_LIVE_OPENAI_IMAGE_SMOKE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                         ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey");
            apiKey.Should().NotBeNullOrWhiteSpace("live smoke requires OPENAI_API_KEY");

            byte[] inputPngBytes;
            using (var img = new Image<Rgba32>(256, 256, new Rgba32(220, 180, 140, 255)))
            using (var ms = new MemoryStream())
            {
                img.Save(ms, new PngEncoder());
                inputPngBytes = ms.ToArray();
            }

            var storagePath = "testing/enhanced/live-smoke/source.png";
            var storage = new Mock<IStorageService>(MockBehavior.Strict);
            storage.Setup(s => s.GetImageAsync(storagePath))
                .ReturnsAsync(new MemoryStream(inputPngBytes));

            var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = apiKey,
                    ["OpenAI:ImageModel"] = Environment.GetEnvironmentVariable("OPENAI_IMAGE_MODEL") ?? "gpt-image-2"
                })
                .Build();

            var service = new OpenAIImageGenerationService(
                new HttpClient(),
                factory.Object,
                configuration,
                NullLogger<OpenAIImageGenerationService>.Instance,
                storage.Object);

            var dataUrl = await service.EnhancePhotoQualityAsync(new EnhancePhotoRequestDto
            {
                ImageStoragePath = storagePath,
                EnhancementType = "headshot"
            });

            (dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
             dataUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }

        private static void AssertMultipartField(string body, string fieldName)
        {
            (body.Contains($"name=\"{fieldName}\"") || body.Contains($"name={fieldName}")).Should().BeTrue();
        }

        private static void AssertNoMultipartField(string body, string fieldName)
        {
            body.Should().NotContain($"name=\"{fieldName}\"");
            body.Should().NotContain($"name={fieldName};");
        }

        private sealed class CaptureHttpMessageHandler : HttpMessageHandler
        {
            public string? LastEditPostContent { get; private set; }

            // Minimal 1x1 transparent PNG
            public byte[]? InputImageBytes { get; set; }
            public int SourceImageGetCount { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Get && request.RequestUri != null && request.RequestUri.Host == "example.com")
                {
                    SourceImageGetCount++;
                    // Return a tiny PNG for the input image
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(InputImageBytes ?? Array.Empty<byte>())
                    };
                    resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    return resp;
                }

                // Edits endpoint
                if (request.Method == HttpMethod.Post && request.RequestUri != null && request.RequestUri.AbsoluteUri.Contains("/images/edits"))
                {
                    var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                    LastEditPostContent = body;

                    // Return a simple OpenAI-like response with a base64 image
                    var openAiResponse = new
                    {
                        created = 123,
                        data = new[]
                        {
                            new { b64_json = Convert.ToBase64String(InputImageBytes ?? Array.Empty<byte>()) }
                        }
                    };
                    var json = JsonSerializer.Serialize(openAiResponse);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Unexpected request in test handler")
                };
            }
        }
    }
}
