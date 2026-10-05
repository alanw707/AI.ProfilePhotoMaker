namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Only licensed adapters may supply observations; the default fails closed.</summary>
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
