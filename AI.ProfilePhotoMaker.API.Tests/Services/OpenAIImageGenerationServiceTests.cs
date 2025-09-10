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
        public async Task EnhancePhotoQualityAsync_SendsModelParameter_GptImage1()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler();
            var httpClient = new HttpClient(handler);

            var configDict = new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key"
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

            // Assert: verify POST to images/edits contains model=gpt-image-1
            handler.LastEditPostContent.Should().NotBeNull();
            var postBody = handler.LastEditPostContent!;
            (postBody.Contains("name=\"model\"") || postBody.Contains("name=model")).Should().BeTrue();
            postBody.Should().Contain("gpt-image-1");

            // Assert: result is data URL
            dataUrl.Should().StartWith("data:image/png;base64,");
        }

        private sealed class CaptureHttpMessageHandler : HttpMessageHandler
        {
            public string? LastEditPostContent { get; private set; }

            // Minimal 1x1 transparent PNG
            public byte[]? InputImageBytes { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Get && request.RequestUri != null && request.RequestUri.Host == "example.com")
                {
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
