using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Tests.Infrastructure;

internal sealed class FakeOpenAiHttpMessageHandler : HttpMessageHandler
{
    public const string SuccessImageUrl = "https://example.com/openai-success.png";
    public const string FailureImageUrl = "https://example.com/openai-missing.png";
    private const string SamplePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAMAAABEpIrGAAACT1BMVEX/////AP+AgICqVaqAQICZZpmAVYCSSZKAYICZTYCLXYuVVYCJTomSW4CIVYiPUICHWoeUUYaSVYaLUYCQWYWKVYCPUoWOVYSSUoCNWISRVYCMUoSPWICLVYOPU4CRU4OQVYOMU4CPV4OOU4KRUYCOVYKQU4CNU4KMVYKOU4SQUoKOVYSQU4KNUoSNVISPUoKOVIKQUoSOVYKPVIONUoGPVYONVIGMVYGOVIOQU4GOVYONU4ONVIOOU4GNVYOOVIGPU4OPVYONVIGNVYGOVIKPU4GOVYKPVIGNU4KPVYGNVIKOU4GNVYKPU4KOVYGPVIKNU4GPVYKNVIOOVIKPU4OOVYKPVIONU4KOU4OOVIOPVIKNVIOOVIKNVYOOVIKPVIOOVYKPVIOOU4KNVIGOVIKPU4GOVIKPVIGOU4KPVIGNVIKPU4KOVIGOU4GNVIGOU4KNVIGOVIKPU4GOVIKPVIGOU4KOVIOOU4ONVIKOVIOPU4KOVIOPVIKOU4ONVIOOU4KNVIOOVIKPU4OOVYKOVIKOVIKNVYKOVIKOVIKOVYKOVIKOVIKNVYKPVIKOVYKOVIKOVIKOVYKOVIKOVIKNVYGPVIGOVYKOVIKOVYGOVIKOVIGNVYKOVIGPVIKOVYOOVIKOVYKNVYOOVIKOU4KOVIKOVIKOU4KOVIKOVIKPU4KOVIKOU4KOVIKOVIKOVIKOVIKPVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVIKOVILT/jDJAAAAxHRSTlMAAQIDBAUGBwgKCwwNDg8QERMVFhcYGRscHR4fICEiJScoKSssLS4xMzQ1Njc4Ojs9Pj9AQUJDRUZHSEpMTU5PUFRVV1hZWltcXV5fYGJjZGVmZ2prbG1ucXN0d3p7fH1+f4GCg4SFhoeIi42OkJKTlJWWl5iZmpydnp+goaKkpaanqKytrq+ws7W2t7i6u7y9vr/AwcPExsfIycrLzM3O0NPV1tfY2drb3N3f4OHj5OXm6Onq6+zt7/Dx8vP2+Pn6/P3+WK8WRgAAAmpJREFUeF5t0OlbjFEch/HvzFRTRkUCkuxaZKdE9j0FJDtCIHshO0REdiglkCDtUoMM9x/mOc9cSvi8OdfvOveLc37qFJyae/ttU+Pr6zsnufSv/gda6VC/rYe6Csr+RhctK/WnqHIA3/2cJakzM/ZV/AQoClWHoXVAW3Yf+SnmyHfgeaT8NLAeuGqPsfHxQ2UZUgpUemRzPwO2ydYMD2QEnQUuybYL2Col7uoM9sbIeRFYJMugdiiU1PPjjN9BxssQKbgS6qxTefA5QpYJDf38wZCWkbKM8kGW5PkCW+RwStp+12UCd2WmJJd0Dl5Is6E9QrG1Bya4AnLDTBC139r6qYZwjQGG6TCUSLuBxvzkgCdQEDL7ghdYLEctrNYj+wtV2E5Gxid0f4ztinQRjqsW5spzvg3jnCzPMBr2S9lQIi8kSXJPP9kCs2TZCB+tF0laA0/VBlNl8yQvCJQlND3RJVsWlKkGFkqKWF78wbH5Tow09lFa74a8ZNPuhGLdgxw5rnsLF4bNgy/j0ny0xw3eVNGyQyqEo8qFh9L4blJ0GzA/B6j2WNNEuZpgmaaBb4AsgY+Bu67Qd8AZGUnwc6Dcn2Cv5N9Vc79jGQnf7S1ZiqFU0h74Okga/QNI1akV2gB4I6UpQJqkXl64HyDH+nYOygTOm7TOkcLfwxv732uBfElxV4PtQH2KoqTgW0CKDGcJcCJIhgls4eb+kPx6VgFlw2WEuGVMfg/cDJKf+r4CfKfj5OdMuoa5D1GHsCKMmrNbVmVuv9yEcShQf0pvoovqFP3Fs66KDmVLA/QfI7IKbleU38hbHq1OvwC3dFhaLHVbewAAAABJRU5ErkJggg==";
    internal static readonly byte[] SamplePngBytes = Convert.FromBase64String(SamplePngBase64);

    public int SourceImageGetCount { get; private set; }

    public void Reset()
    {
        SourceImageGetCount = 0;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            SourceImageGetCount++;

            if (request.RequestUri is not null &&
                string.Equals(request.RequestUri.ToString(), SuccessImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(SamplePngBytes)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found", Encoding.UTF8)
            });
        }

        if (request.Method == HttpMethod.Post &&
            request.RequestUri is not null &&
            string.Equals(request.RequestUri.Host, "api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            var responseJson = $"{{\"data\":[{{\"b64_json\":\"{SamplePngBase64}\"}}]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly FakeOpenAiHttpMessageHandler _handler;

    public FakeHttpClientFactory(FakeOpenAiHttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}
