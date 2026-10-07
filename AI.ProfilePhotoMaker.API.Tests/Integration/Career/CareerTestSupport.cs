using System.Net.Http.Headers;
using AI.ProfilePhotoMaker.API.Services.Career;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// The standard test host with the career workspace flag switched on. The flag is
/// supplied as in-memory configuration (not an environment variable) so it cannot
/// leak into the flag-off factories that run in parallel.
/// </summary>
public class CareerWorkspaceEnabledFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:CareerWorkspace"] = "true"
            }));
    }
}

/// <summary>
/// The standard test host with the career flag explicitly OFF, so flag-off assertions
/// hold even when the environment sets Features__CareerWorkspace=true.
/// </summary>
public class CareerWorkspaceDisabledFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:CareerWorkspace"] = "false"
            }));
    }
}

/// <summary>
/// A signed-in test user. Each test creates its own user ID so tests sharing one
/// in-memory database never see each other's career data.
/// </summary>
public sealed class CareerClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    public string UserId { get; }

    public CareerClient(CustomWebApplicationFactory factory, string? userId = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        UserId = userId ?? $"career-user-{Guid.NewGuid():N}";
        _http = factory.CreateAuthenticatedClient();
        _http.DefaultRequestHeaders.Add("X-Test-UserId", UserId);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            _http.DefaultRequestHeaders.Add(name, value);
        }
    }

    public static object ValidProfile(string title = "Data analyst") => new
    {
        currentTitle = title,
        industry = "Healthcare",
        yearsExperience = 4,
        location = "Denver, CO",
        summary = "Builds reporting for clinical operations.",
        skills = new[] { "SQL", "Tableau" },
        highlights = new[] { "Built the weekly KPI report" },
        workArrangement = "hybrid",
        confirmed = true
    };

    public static object ValidGoal(string role = "Senior data analyst") => new
    {
        targetRole = role,
        targetLocation = "Seattle, WA",
        workArrangement = "remote",
        desiredPayMin = 95000,
        desiredPayMax = 120000,
        weeklyEffortHours = 5,
        confirmed = true
    };

    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(path);

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }
        if (ifMatch != null)
        {
            // Raw, so tests can send values such as * or weak tags as clients would.
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        return _http.SendAsync(request);
    }

    /// <summary>Multipart upload to POST /api/career/resumes.</summary>
    public Task<HttpResponseMessage> UploadResumeAsync(
        byte[]? bytes, string fileName = "resume.pdf", string? consent = "true",
        string? consentVersion = ResumeImportService.DefaultConsentVersion)
    {
        var form = new MultipartFormDataContent();
        if (bytes != null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", fileName);
        }
        if (consent != null)
        {
            form.Add(new StringContent(consent), "consent");
        }
        if (consentVersion != null)
        {
            form.Add(new StringContent(consentVersion), "consentVersion");
        }
        return _http.PostAsync("/api/career/resumes", form);
    }

    /// <summary>Uploads and returns the 201 body.</summary>
    public async Task<JsonElement> UploadOkAsync(byte[] bytes, string fileName = "resume.pdf") =>
        await ReadDataAsync(await UploadResumeAsync(bytes, fileName), 201);

    public Task<HttpResponseMessage> PostPasteAsync(string text) =>
        SendAsync(HttpMethod.Post, "/api/career/profile/proposals", new { text });

    public Task<HttpResponseMessage> AcceptAsync(string proposalId, IEnumerable<string> itemIds, string? ifMatch) =>
        SendAsync(HttpMethod.Post, $"/api/career/profile/proposals/{proposalId}/accept", new { itemIds }, ifMatch);

    public Task<HttpResponseMessage> PutProfileAsync(object body, string? ifMatch = null) =>
        SendAsync(HttpMethod.Put, "/api/career/profile", body, ifMatch);

    public Task<HttpResponseMessage> PostGoalAsync(object body) =>
        SendAsync(HttpMethod.Post, "/api/career/goals", body);

    public Task<HttpResponseMessage> PatchGoalAsync(string id, object body, string? ifMatch) =>
        SendAsync(HttpMethod.Patch, $"/api/career/goals/{id}", body, ifMatch);

    /// <summary>Creates a profile and returns its body.</summary>
    public async Task<JsonElement> CreateProfileAsync(string title = "Data analyst")
    {
        var response = await PutProfileAsync(ValidProfile(title));
        return await ReadDataAsync(response, 200);
    }

    public async Task<JsonElement> CreateGoalAsync(string role = "Senior data analyst")
    {
        var response = await PostGoalAsync(ValidGoal(role));
        return await ReadDataAsync(response, 201);
    }

    public static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Asserts the status code and returns the envelope's data.</summary>
    public static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response, int expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expectedStatus)
        {
            throw new Xunit.Sdk.XunitException($"Expected {expectedStatus} but got {(int)response.StatusCode}: {body}");
        }
        var root = JsonDocument.Parse(body).RootElement.Clone();
        return root.GetProperty("data");
    }

    /// <summary>Asserts the status code and returns the envelope's error object.</summary>
    public static async Task<JsonElement> ReadErrorAsync(HttpResponseMessage response, int expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expectedStatus)
        {
            throw new Xunit.Sdk.XunitException($"Expected {expectedStatus} but got {(int)response.StatusCode}: {body}");
        }
        var root = JsonDocument.Parse(body).RootElement.Clone();
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        return root.GetProperty("error");
    }
}
