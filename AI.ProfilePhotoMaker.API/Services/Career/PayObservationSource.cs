namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Only licensed adapters may supply observations; the default fails closed. An adapter must
/// return rows whose <c>Role</c> is the requested occupation code and whose <c>Geography</c> is
/// the requested area code, because the cohort filters compare on exactly those keys.
/// </summary>
public interface IPayObservationSource
{
    string? SourceId { get; }

    IReadOnlyList<PayObservation> ObservationsFor(string occupationCode, string areaCode);
}

public sealed class NoQualifiedPayObservationSource : IPayObservationSource
{
    public string? SourceId => null;
    public IReadOnlyList<PayObservation> ObservationsFor(string occupationCode, string areaCode) => Array.Empty<PayObservation>();
}
