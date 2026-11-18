using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Models;

namespace PRism.Api.Services;

/// <summary>
/// Production-ready alternative to <see cref="GeminiReviewService"/> using Azure OpenAI
/// Chat Completions with structured outputs (json_schema). Enable by setting Bot:AiProvider
/// to "AzureOpenAi" and pointing the AzureOpenAi* settings at a deployment that supports
/// structured outputs (e.g. gpt-4o 2024-08-06 or later).
/// </summary>
public class AzureOpenAiReviewService : IAiReviewService
{
    private readonly HttpClient _httpClient;
    private readonly BotOptions _options;
    private readonly ILogger _logger;

    public AzureOpenAiReviewService(HttpClient httpClient, IOptions<BotOptions> options, ILogger<AzureOpenAiReviewService>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? NullLogger<AzureOpenAiReviewService>.Instance;
    }

    public async Task<string> ReviewAsync(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return ReviewPrompts.NothingToReviewMessage;
        }

        var request = AzureOpenAiChatRequest.ForReview(diff);

        var url = $"{_options.AzureOpenAiEndpoint.TrimEnd('/')}/openai/deployments/{_options.AzureOpenAiDeployment}/chat/completions?api-version={_options.AzureOpenAiApiVersion}";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };
        requestMessage.Headers.Add("api-key", _options.AzureOpenAiApiKey);

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AzureOpenAiChatResponse>();
        var text = result?.Choices?.FirstOrDefault()?.Message?.Content;

        return ReviewCommentBuilder.Build(text, diff, _logger);
    }
}
