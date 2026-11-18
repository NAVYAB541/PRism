using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Models;

namespace PRism.Api.Services;

public class GeminiReviewService : IAiReviewService
{
    private readonly HttpClient _httpClient;
    private readonly BotOptions _options;
    private readonly ILogger _logger;

    public GeminiReviewService(HttpClient httpClient, IOptions<BotOptions> options, ILogger<GeminiReviewService>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? NullLogger<GeminiReviewService>.Instance;
    }

    public async Task<string> ReviewAsync(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return ReviewPrompts.NothingToReviewMessage;
        }

        var request = GeminiRequest.ForReview(diff);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.GeminiModel}:generateContent";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };
        // Header rather than ?key= so the API key never appears in URLs or request logs.
        requestMessage.Headers.Add("x-goog-api-key", _options.GeminiApiKey);

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        return ReviewCommentBuilder.Build(text, diff, _logger);
    }
}
