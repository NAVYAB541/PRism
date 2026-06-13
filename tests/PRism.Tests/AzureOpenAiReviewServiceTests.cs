using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class AzureOpenAiReviewServiceTests
{
    private static BotOptions CreateOptions() => new()
    {
        GitHubToken = "gh-token",
        WebhookSecret = "secret",
        AzureOpenAiEndpoint = "https://example.openai.azure.com",
        AzureOpenAiApiKey = "azure-key",
        AzureOpenAiDeployment = "gpt-4o",
        AzureOpenAiApiVersion = "2024-08-01-preview"
    };

    [Fact]
    public async Task ReviewAsync_ReturnsNothingToReview_ForEmptyDiff()
    {
        var handler = new FakeHttpMessageHandler("{}");
        var service = new AzureOpenAiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync("   ");

        Assert.Equal(ReviewPrompts.NothingToReviewMessage, result);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task ReviewAsync_SendsDiffAndParsesResponse()
    {
        const string responseJson = """
        {
          "choices": [
            { "message": { "role": "assistant", "content": "Looks good overall." } }
          ]
        }
        """;
        var handler = new FakeHttpMessageHandler(responseJson);
        var service = new AzureOpenAiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync("--- a.cs ---\n+ added line");

        Assert.Equal("Looks good overall.", result);
        Assert.Contains("example.openai.azure.com/openai/deployments/gpt-4o/chat/completions", handler.LastRequestUri!.ToString());
        Assert.Contains("api-version=2024-08-01-preview", handler.LastRequestUri!.ToString());
        Assert.Contains("added line", handler.LastRequestContent);
    }
}
