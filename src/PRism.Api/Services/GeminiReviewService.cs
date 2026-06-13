using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Models;

namespace PRism.Api.Services;

public class GeminiReviewService : IAiReviewService
{
    private readonly HttpClient _httpClient;
    private readonly BotOptions _options;

    public GeminiReviewService(HttpClient httpClient, IOptions<BotOptions> options)
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

        var prompt = $"{ReviewPrompts.SystemPrompt}\n\n{ReviewPrompts.BuildUserPrompt(diff)}";
        var request = GeminiRequest.ForPrompt(prompt);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.GeminiModel}:generateContent?key={_options.GeminiApiKey}";

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();

        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
            ?? "PRism: The AI provider returned an empty response.";
    }
}
