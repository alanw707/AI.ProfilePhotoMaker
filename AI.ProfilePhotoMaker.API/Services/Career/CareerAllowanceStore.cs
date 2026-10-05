using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Month and settlement rules for the career run allowance (ADR 0009), shared by the run service and the runner.</summary>
internal static class CareerAllowanceStore
{
    public static DateTime PeriodStart(DateTime utc) => new(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Reserve(CareerAllowance allowance)
    {
        allowance.Reserved++;
        allowance.Version++;
    }

    /// <summary>
    /// Ends a reservation: the unit is spent once a model call was made, otherwise it
    /// goes back. Used by completion, failure and cancel alike.
    /// </summary>
    public static void Settle(CareerAllowance allowance, bool modelCalled)
    {
        if (allowance.Reserved > 0)
        {
            allowance.Reserved--;
        }
        if (modelCalled)
        {
            allowance.Used++;
        }
        allowance.Version++;
    }
}
