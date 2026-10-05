namespace AI.ProfilePhotoMaker.API.Services.Career;

// Request and response shapes for docs/career/api-agent-runs.md.

public sealed class CreateCareerRunRequest
{
    public string? Task { get; set; }

    /// <summary>targeted_resume only: propose changes to this resume instead of creating a new one.</summary>
    public Guid? MaterialId { get; set; }
}

public sealed class AnswerCareerRunRequest
{
    public string? QuestionId { get; set; }
    public string? Answer { get; set; }
}

public sealed record CareerRunStepDto(int Ordinal, string Kind, string Name, string Label, string Status, DateTime? CompletedAt);

public sealed record CareerRunChoiceDto(string Value, string Label);

/// <summary>A question the run waits on. <see cref="Choices"/> is omitted for free-text questions.</summary>
public sealed record CareerRunQuestionDto(
    string Id,
    string Text,
    int MaxLength,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CareerRunChoiceDto>? Choices = null);

public sealed record CareerAllowanceDto(int Used, int Reserved, int Limit, DateTime PeriodStart);

public sealed record CareerAgentRunDto(
    Guid Id,
    string Task,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    int? PinnedProfileVersion,
    int? PinnedGoalVersion,
    IReadOnlyList<CareerRunStepDto> Steps,
    CareerRunQuestionDto? Question,
    Guid? ProposalId,
    Guid? OccupationMatchId,
    Guid? MarketBriefId,
    bool ProfileChanged,
    string? ErrorCode,
    CareerAllowanceDto Allowance,
    Guid? PayAnalysisId = null,
    Guid? RoadmapId = null,
    Guid? MaterialId = null);

public sealed record CareerRunListDto(IReadOnlyList<CareerAgentRunDto> Runs, CareerAllowanceDto Allowance);

public static class CareerAgentErrorCodes
{
    public const string RunNotFound = "CareerRunNotFound";
    public const string RunNotWaiting = "CareerRunNotWaiting";
    public const string IdempotencyMismatch = "CareerIdempotencyMismatch";
    public const string ProfileRequired = "CareerProfileRequired";
    public const string AllowanceExhausted = "CareerAllowanceExhausted";
    public const string ModelUnavailable = "CareerModelUnavailable";
    public const string ReferenceUnavailable = "CareerReferenceUnavailable";

    // Failure codes stored on a failed run.
    public const string StepLimit = "CareerStepLimit";
    public const string TimeLimit = "CareerTimeLimit";
    public const string RetryLimit = "CareerRetryLimit";
    public const string CostLimit = "CareerCostLimit";
    public const string ToolNotAllowed = "CareerToolNotAllowed";
    public const string QuestionExpired = "CareerQuestionExpired";
    public const string ModelFailed = "CareerModelFailed";
}
