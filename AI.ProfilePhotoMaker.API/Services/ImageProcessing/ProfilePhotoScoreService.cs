using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class ProfilePhotoScoreService : IProfilePhotoScoreService
{
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<ProfilePhotoScoreService>? _logger;

    public ProfilePhotoScoreService()
    {
    }

    public ProfilePhotoScoreService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ProfilePhotoScoreService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProfilePhotoScoreDto> ScoreAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        await using var buffered = new MemoryStream();
        await imageStream.CopyToAsync(buffered, cancellationToken);
        var imageBytes = buffered.ToArray();
        buffered.Position = 0;

        using var image = await Image.LoadAsync<Rgba32>(buffered, cancellationToken);
        var resolutionScore = ScoreResolution(image.Width, image.Height);
        var framingScore = ScoreFraming(image.Width, image.Height);
        var lightingScore = ScoreLighting(image);
        var sharpnessScore = ScoreSharpness(image);
        var backgroundScore = ScoreBackgroundSimplicity(image);
        var facePresenceScore = ScoreFacePresence(image);
        var platformFitScore = (framingScore + resolutionScore + facePresenceScore) / 3;
        var heuristicProfessionalism = Math.Clamp((lightingScore + backgroundScore + sharpnessScore) / 3, 45, 96);
        var heuristicApproachability = Math.Clamp((facePresenceScore + lightingScore + framingScore) / 3, 45, 96);
        var heuristicConfidence = Math.Clamp((facePresenceScore + sharpnessScore + platformFitScore) / 3, 45, 96);
        var heuristicAttireBackgroundFit = Math.Clamp((backgroundScore + sharpnessScore + facePresenceScore) / 3, 45, 96);

        var rubric = await TryScoreSubjectiveRubricAsync(imageBytes, fileName, cancellationToken);
        var professionalismScore = rubric?.Professionalism ?? heuristicProfessionalism;
        var approachabilityScore = rubric?.Approachability ?? heuristicApproachability;
        var confidenceScore = rubric?.Confidence ?? heuristicConfidence;
        var attireBackgroundFitScore = rubric?.AttireBackgroundFit ?? heuristicAttireBackgroundFit;
        var roleFitScore = rubric?.RoleFit ?? Math.Min(94, Math.Max(55, (professionalismScore + approachabilityScore + confidenceScore + attireBackgroundFitScore) / 4));
        var rubricSource = rubric == null ? "Heuristic fallback" : "AI rubric";

        var subscores = new List<ProfilePhotoSubscoreDto>
        {
            BuildSubscore("resolution", "Resolution", resolutionScore, resolutionScore >= 80 ? "Large enough for high-quality exports." : "Use a larger, sharper source photo for best exports."),
            BuildSubscore("crop", "Crop and framing", framingScore, framingScore >= 80 ? "Aspect ratio works well for profile crops." : "A square or portrait image will export more cleanly."),
            BuildSubscore("lighting", "Lighting", lightingScore, lightingScore >= 80 ? "Lighting looks balanced." : "Try softer, brighter lighting with fewer harsh shadows."),
            BuildSubscore("sharpness", "Sharpness", sharpnessScore, sharpnessScore >= 80 ? "Image appears crisp enough." : "Use a less blurry source photo."),
            BuildSubscore("background", "Background", backgroundScore, backgroundScore >= 80 ? "Background looks reasonably clean." : "A simpler background will look more professional."),
            BuildSubscore("face_presence", "Face presence", facePresenceScore, facePresenceScore >= 80 ? "Face-like central subject signal is strong enough for profile use." : "Use a clearer front-facing photo where your face is larger and unobstructed."),
            BuildSubscore("platform_fit", "Platform fit", platformFitScore, platformFitScore >= 80 ? "Good fit for LinkedIn and avatar exports." : "May need crop/zoom adjustment before export."),
            BuildSubscore("professionalism", $"Professionalism ({rubricSource})", professionalismScore, rubric?.ProfessionalismFeedback ?? (professionalismScore >= 80 ? "Professional impression is strong for profile use." : "Improve background simplicity, lighting, and crispness to look more professional.")),
            BuildSubscore("approachability", $"Approachability ({rubricSource})", approachabilityScore, rubric?.ApproachabilityFeedback ?? (approachabilityScore >= 80 ? "Approachable profile impression is strong." : "Use clearer face framing and softer light for a more approachable profile photo.")),
            BuildSubscore("confidence", $"Confidence ({rubricSource})", confidenceScore, rubric?.ConfidenceFeedback ?? (confidenceScore >= 80 ? "Confident presentation is strong." : "Use a sharper, well-framed image with a stronger central subject.")),
            BuildSubscore("attire_background_fit", $"Attire/background fit ({rubricSource})", attireBackgroundFitScore, rubric?.AttireBackgroundFitFeedback ?? (attireBackgroundFitScore >= 80 ? "Subject/background combination is appropriate for professional roles." : "Choose a cleaner background and clearer subject presentation for role fit.")),
            BuildSubscore("role_readiness", $"Role readiness ({rubricSource})", roleFitScore, rubric?.RoleFitFeedback ?? (roleFitScore >= 80 ? "The image has a strong baseline for professional role-specific positioning." : "Improve professionalism, approachability, confidence, and attire/background fit before relying on role-specific styling."))
        };

        // Technical signals gate unusable uploads; derived subscores remain explanatory and are not counted twice.
        var technicalScore = new[] { resolutionScore, framingScore, lightingScore, sharpnessScore, backgroundScore, facePresenceScore }.Average();
        var readinessScore = new[] { professionalismScore, approachabilityScore, confidenceScore, attireBackgroundFitScore, roleFitScore }.Average();
        var overall = CalibrateProfessionalReadiness((technicalScore * 0.4) + (readinessScore * 0.6));
        var strengths = subscores.Where(s => s.Score >= 82).Select(s => s.Label).Take(3).ToList();
        var improvements = subscores.Where(s => s.Score < 82).OrderBy(s => s.Score).Select(s => s.Feedback).Take(3).ToList();

        return new ProfilePhotoScoreDto
        {
            OverallScore = overall,
            RatingLabel = overall >= 90 ? "LinkedIn-ready" : overall >= 75 ? "Good starting point" : "Needs improvement",
            Subscores = subscores,
            Strengths = strengths.Count > 0 ? strengths : new List<string> { "Clear enough to start a profile-photo workflow" },
            Improvements = improvements.Count > 0 ? improvements : new List<string> { "Use photo adjustment or refinement to tune the final result." },
            Guidance = rubric?.OverallFeedback ?? BuildGuidance(overall, rubric != null),
            QualityGate = BuildQualityGate(image.Width, image.Height, resolutionScore, framingScore, lightingScore, sharpnessScore, facePresenceScore, platformFitScore)
        };
    }

    private static PhotoQualityGateDto BuildQualityGate(
        int width,
        int height,
        int resolutionScore,
        int framingScore,
        int lightingScore,
        int sharpnessScore,
        int facePresenceScore,
        int platformFitScore)
    {
        var blocked = new List<string>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        if (width < 512 || height < 512 || resolutionScore < 45)
        {
            blocked.Add("Image resolution is too low for reliable portrait generation.");
            recommendations.Add("Upload a larger photo, ideally at least 1024×1024.");
        }

        if (facePresenceScore < 35)
        {
            blocked.Add("No clear single portrait subject was detected.");
            recommendations.Add("Upload a clear front-facing photo with one person visible.");
        }
        else if (facePresenceScore < 58)
        {
            warnings.Add("Face presence is weak; the face may be too small, obstructed, or off-center.");
            recommendations.Add("Use a photo where your face is larger and unobstructed.");
        }

        if (sharpnessScore < 35)
        {
            blocked.Add("Image appears too blurry for reliable generation.");
            recommendations.Add("Upload a sharper source photo.");
        }
        else if (sharpnessScore < 55)
        {
            warnings.Add("Image sharpness is below ideal quality.");
            recommendations.Add("Use a less blurry photo for better facial detail.");
        }

        if (lightingScore < 35)
        {
            blocked.Add("Lighting is too poor for reliable generation.");
            recommendations.Add("Upload a brighter, evenly lit photo.");
        }
        else if (lightingScore < 62)
        {
            warnings.Add("Lighting may be uneven or too dim.");
            recommendations.Add("Try softer, brighter lighting with fewer harsh shadows.");
        }

        if (framingScore < 45 || platformFitScore < 45)
        {
            warnings.Add("Framing may not work well for a single professional portrait.");
            recommendations.Add("Use a centered head-and-shoulders photo with one person.");
        }

        return new PhotoQualityGateDto
        {
            Status = blocked.Count > 0 ? "blocked" : warnings.Count > 0 ? "warning" : "pass",
            Reasons = blocked.Count > 0 ? blocked : warnings,
            Recommendations = recommendations.Distinct().Take(4).ToList()
        };
    }

    private async Task<AiRubricScore?> TryScoreSubjectiveRubricAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? _configuration?["OpenAI:ApiKey"];
        if (_httpClientFactory == null || string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var model = _configuration?["OpenAI:VisionModel"] ?? "gpt-4o-mini";
            var mime = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(imageBytes)}";
            var payload = new
            {
                model,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Score this LinkedIn professional profile photo. Return JSON only with integer 0-100 fields professionalism, approachability, confidence, attireBackgroundFit, roleFit and concise feedback fields professionalismFeedback, approachabilityFeedback, confidenceFeedback, attireBackgroundFitFeedback, roleFitFeedback, overallFeedback. Judge visible professionalism, approachability, confidence, attire appropriateness, background fit, and LinkedIn role readiness. Use this calibration: 90-95 = polished, clear, platform-ready professional portrait; 80-89 = good with a minor shortcoming; 70-79 = a material issue; below 70 = unsuitable. Neat business-casual attire is appropriate for LinkedIn unless visibly unprofessional. Do not identify the person." },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("AI profile rubric scoring failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return null;

            using var rubricDoc = JsonDocument.Parse(content);
            var root = rubricDoc.RootElement;
            return new AiRubricScore
            {
                Professionalism = ReadScore(root, "professionalism"),
                Approachability = ReadScore(root, "approachability"),
                Confidence = ReadScore(root, "confidence"),
                AttireBackgroundFit = ReadScore(root, "attireBackgroundFit"),
                RoleFit = ReadScore(root, "roleFit"),
                ProfessionalismFeedback = ReadString(root, "professionalismFeedback"),
                ApproachabilityFeedback = ReadString(root, "approachabilityFeedback"),
                ConfidenceFeedback = ReadString(root, "confidenceFeedback"),
                AttireBackgroundFitFeedback = ReadString(root, "attireBackgroundFitFeedback"),
                RoleFitFeedback = ReadString(root, "roleFitFeedback"),
                OverallFeedback = ReadString(root, "overallFeedback")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "AI profile rubric scoring failed; using deterministic fallback.");
            return null;
        }
    }

    private static int ReadScore(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var score) ? Math.Clamp(score, 0, 100) : 70;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() : null;

    private sealed class AiRubricScore
    {
        public int Professionalism { get; init; }
        public int Approachability { get; init; }
        public int Confidence { get; init; }
        public int AttireBackgroundFit { get; init; }
        public int RoleFit { get; init; }
        public string? ProfessionalismFeedback { get; init; }
        public string? ApproachabilityFeedback { get; init; }
        public string? ConfidenceFeedback { get; init; }
        public string? AttireBackgroundFitFeedback { get; init; }
        public string? RoleFitFeedback { get; init; }
        public string? OverallFeedback { get; init; }
    }

    private static int CalibrateProfessionalReadiness(double rawScore)
    {
        if (rawScore < 55)
        {
            return (int)Math.Round(rawScore);
        }

        if (rawScore < 70)
        {
            return (int)Math.Round(55 + ((rawScore - 55) * 4 / 3));
        }

        return Math.Min(100, (int)Math.Round(75 + ((rawScore - 70) * 25 / 18)));
    }

    private static ProfilePhotoSubscoreDto BuildSubscore(string code, string label, int score, string feedback)
    {
        return new ProfilePhotoSubscoreDto
        {
            Code = code,
            Label = label,
            Score = Math.Clamp(score, 0, 100),
            Feedback = feedback
        };
    }

    private static int ScoreResolution(int width, int height)
    {
        var shortest = Math.Min(width, height);
        if (shortest >= 1200) return 96;
        if (shortest >= 900) return 88;
        if (shortest >= 700) return 78;
        if (shortest >= 512) return 66;
        return 48;
    }

    private static int ScoreFraming(int width, int height)
    {
        var ratio = width / (double)height;
        var idealDistance = Math.Min(Math.Abs(ratio - 1.0), Math.Abs(ratio - 0.8));
        var score = 100 - (int)Math.Round(idealDistance * 80);
        return Math.Clamp(score, 45, 96);
    }

    private static int ScoreLighting(Image<Rgba32> image)
    {
        var (mean, stdDev, _) = SampleLuminance(image);
        var exposureScore = 100 - Math.Abs(mean - 0.55) * 140;
        var contrastScore = Math.Clamp(stdDev * 260, 35, 100);
        return Math.Clamp((int)Math.Round((exposureScore * 0.65) + (contrastScore * 0.35)), 35, 96);
    }

    private static int ScoreSharpness(Image<Rgba32> image)
    {
        var stepX = Math.Max(1, image.Width / 80);
        var stepY = Math.Max(1, image.Height / 80);
        double edgeTotal = 0;
        var samples = 0;

        for (var y = stepY; y < image.Height; y += stepY)
        {
            for (var x = stepX; x < image.Width; x += stepX)
            {
                var current = Luminance(image[x, y]);
                var previous = Luminance(image[x - stepX, y - stepY]);
                edgeTotal += Math.Abs(current - previous);
                samples++;
            }
        }

        if (samples == 0) return 50;
        var edgeMean = edgeTotal / samples;
        return Math.Clamp((int)Math.Round(45 + edgeMean * 220), 42, 95);
    }

    private static int ScoreBackgroundSimplicity(Image<Rgba32> image)
    {
        var (_, _, variance) = SampleBorderColorVariance(image);
        var score = 100 - variance * 220;
        return Math.Clamp((int)Math.Round(score), 45, 94);
    }

    private static int ScoreFacePresence(Image<Rgba32> image)
    {
        var centerLeft = image.Width / 4;
        var centerRight = image.Width * 3 / 4;
        var centerTop = image.Height / 8;
        var centerBottom = image.Height * 7 / 8;
        var stepX = Math.Max(1, image.Width / 80);
        var stepY = Math.Max(1, image.Height / 80);
        var skinLike = 0;
        var total = 0;

        for (var y = centerTop; y < centerBottom; y += stepY)
        {
            for (var x = centerLeft; x < centerRight; x += stepX)
            {
                total++;
                if (IsSkinLike(image[x, y])) skinLike++;
            }
        }

        if (total == 0) return 45;
        var ratio = skinLike / (double)total;
        if (ratio >= 0.12 && ratio <= 0.55) return 90;
        if (ratio >= 0.07 && ratio <= 0.65) return 78;
        if (ratio >= 0.03 && ratio <= 0.75) return 62;
        return 45;
    }

    private static bool IsSkinLike(Rgba32 pixel)
    {
        var r = pixel.R;
        var g = pixel.G;
        var b = pixel.B;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        return r > 60 && g > 35 && b > 20 && max - min > 10 && r > g && r > b;
    }

    private static (double Mean, double StdDev, double Variance) SampleLuminance(Image<Rgba32> image)
    {
        var values = new List<double>();
        var stepX = Math.Max(1, image.Width / 64);
        var stepY = Math.Max(1, image.Height / 64);

        for (var y = 0; y < image.Height; y += stepY)
        {
            for (var x = 0; x < image.Width; x += stepX)
            {
                values.Add(Luminance(image[x, y]));
            }
        }

        return Summarize(values);
    }

    private static (double Mean, double StdDev, double Variance) SampleBorderColorVariance(Image<Rgba32> image)
    {
        var values = new List<double>();
        var stepX = Math.Max(1, image.Width / 60);
        var stepY = Math.Max(1, image.Height / 60);
        var borderX = Math.Max(1, image.Width / 8);
        var borderY = Math.Max(1, image.Height / 8);

        for (var y = 0; y < image.Height; y += stepY)
        {
            for (var x = 0; x < image.Width; x += stepX)
            {
                var isBorder = x < borderX || x > image.Width - borderX || y < borderY || y > image.Height - borderY;
                if (isBorder) values.Add(Luminance(image[x, y]));
            }
        }

        return Summarize(values);
    }

    private static (double Mean, double StdDev, double Variance) Summarize(List<double> values)
    {
        if (values.Count == 0) return (0.5, 0, 0);
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return (mean, Math.Sqrt(variance), variance);
    }

    private static double Luminance(Rgba32 pixel)
    {
        return ((0.2126 * pixel.R) + (0.7152 * pixel.G) + (0.0722 * pixel.B)) / 255.0;
    }

    private static string BuildGuidance(int overall, bool aiRubricApplied)
    {
        var prefix = aiRubricApplied ? "AI rubric and image-quality checks" : "Image-quality checks";
        if (overall >= 85) return $"{prefix} rate this photo as strong enough for a professional profile package. Use export crops or small refinements.";
        if (overall >= 70) return $"{prefix} show this photo can work well after generation/refinement. Improve the weakest subscores first.";
        return $"{prefix} recommend a clearer, brighter source photo or a new instant headshot before final exports.";
    }
}
