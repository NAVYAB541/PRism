using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class GeminiReviewServiceTests
{
    private static BotOptions CreateOptions() => new()
    {
        GitHubToken = "gh-token",
        WebhookSecret = "secret",
        GeminiApiKey = "gemini-key",
        GeminiModel = "gemini-2.0-flash"
    };

    [Fact]
    public async Task ReviewAsync_ReturnsNothingToReview_ForEmptyDiff()
    {
        var handler = new FakeHttpMessageHandler("{}");
        var service = new GeminiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync("");

        Assert.Equal(ReviewPrompts.NothingToReviewMessage, result);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task ReviewAsync_SendsDiffAndParsesResponse()
    {
        const string responseJson = """
        {
          "candidates": [
            { "content": { "parts": [ { "text": "Looks good overall." } ] } }
          ]
        }
        """;
        var handler = new FakeHttpMessageHandler(responseJson);
        var service = new GeminiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync("--- a.cs ---\n+ added line");

        Assert.Equal("Looks good overall.", result);
        Assert.Contains("generativelanguage.googleapis.com", handler.LastRequestUri!.ToString());
        Assert.Contains("gemini-2.0-flash", handler.LastRequestUri!.ToString());
        Assert.Contains("added line", handler.LastRequestContent);
    }
}
