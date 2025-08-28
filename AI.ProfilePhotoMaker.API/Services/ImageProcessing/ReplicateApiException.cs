using System.Net;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class ReplicateApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }
    public string RawContent { get; }

    public ReplicateApiException(HttpStatusCode statusCode, string errorCode, string message, string rawContent = "")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RawContent = rawContent ?? string.Empty;
    }
}

