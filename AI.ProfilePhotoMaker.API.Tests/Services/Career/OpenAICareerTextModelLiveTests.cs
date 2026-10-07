using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// One real call to OpenAI, skipped unless OPENAI_API_KEY and CAREER_AGENT_MODEL are set
/// (the model name is never written into code). Costs a fraction of a cent per run.
///   OPENAI_API_KEY=... CAREER_AGENT_MODEL=... dotnet test --filter Category=LiveOpenAI
/// </summary>
[Trait("Category", "LiveOpenAI")]
public class OpenAICareerTextModelLiveTests
{
    [LiveOpenAIFact]
    public async Task DraftsASummaryFromSyntheticFacts()
    {
        var options = new OpenAICareerTextModelOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
            Model = Environment.GetEnvironmentVariable("CAREER_AGENT_MODEL")!,
            InputUsdPerMillionTokens = 0,
            OutputUsdPerMillionTokens = 0,
            RequestTimeout = CareerAgentOptions.ModelCallTimeoutFor(new CareerAgentOptions().LeaseSeconds)
        };
        var model = new OpenAICareerTextModel(new HttpClient(), options, NullLogger<OpenAICareerTextModel>.Instance);
        var request = new CareerModelRequest(
            "profile_summary",
            new CareerProfileFactsDto("Operations analyst", "Logistics", 5, "Columbus, OH", null,
                new List<string> { "SQL", "Process mapping" }, new List<string> { "Cut late shipments by 12 percent" }, "hybrid"),
            new CareerGoalFactsDto("Operations manager", "Columbus, OH", "hybrid", null, null, 5),
            new[] { "read_profile", "read_goal" },
            null);

        var result = await model.CompleteAsync(request);

        result.ToolCall.Should().BeNull();
        result.FinalText.Should().NotBeNullOrWhiteSpace();
        result.FinalText!.Length.Should().BeLessThanOrEqualTo(CareerInputValidator.MaxSummary);
        result.UsageTokens.Should().BeGreaterThan(0);
    }
}

/// <summary>A fact that skips itself unless the live OpenAI settings are in the environment.</summary>
public sealed class LiveOpenAIFactAttribute : FactAttribute
{
    public LiveOpenAIFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAREER_AGENT_MODEL")))
        {
            Skip = "Set OPENAI_API_KEY and CAREER_AGENT_MODEL to run the live OpenAI smoke test.";
        }
    }
}
