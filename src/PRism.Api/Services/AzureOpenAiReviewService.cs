using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Models;

namespace PRism.Api.Services;

/// <summary>
/// Production-ready alternative to <see cref="GeminiReviewService"/> using Azure OpenAI
/// Chat Completions. Enable by setting Bot:AiProvider to "AzureOpenAi" and configuring
/// the AzureOpenAi* settings to point at an Azure OpenAI resource and deployment.
/// </summary>
public class AzureOpenAiReviewService : IAiReviewService
{
    private readonly HttpClient _httpClient;
    private readonly BotOptions _options;

    public AzureOpenAiReviewService(HttpClient httpClient, IOptions<BotOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> ReviewAsync(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return ReviewPrompts.NothingToReviewMessage;
        }

        var request = AzureOpenAiChatRequest.ForPrompt(ReviewPrompts.SystemPrompt, ReviewPrompts.BuildUserPrompt(diff));

        var url = $"{_options.AzureOpenAiEndpoint.TrimEnd('/')}/openai/deployments/{_options.AzureOpenAiDeployment}/chat/completions?api-version={_options.AzureOpenAiApiVersion}";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };
        requestMessage.Headers.Add("api-key", _options.AzureOpenAiApiKey);

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AzureOpenAiChatResponse>();

        return result?.Choices?.FirstOrDefault()?.Message?.Content
            ?? "PRism: The AI provider returned an empty response.";
    }
}
