using System.Net;

namespace PRism.Tests;

/// <summary>An <see cref="HttpMessageHandler"/> that records the last request and returns a fixed response.</summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseContent;

    public string? LastRequestContent { get; private set; }
    public Uri? LastRequestUri { get; private set; }
    public System.Net.Http.Headers.HttpRequestHeaders? LastRequestHeaders { get; private set; }

    public FakeHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastRequestHeaders = request.Headers;
        LastRequestContent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        };
    }
}
