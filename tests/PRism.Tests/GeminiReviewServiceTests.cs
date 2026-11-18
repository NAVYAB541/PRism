using System.Text.Json;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class GeminiReviewServiceTests
{
    private const string Diff = "--- a.cs ---\n@@ -1 +1,2 @@\n+ added line";

    private static BotOptions CreateOptions() => new()
    {
        GitHubToken = "gh-token",
        WebhookSecret = "secret",
        GeminiApiKey = "gemini-key",
        GeminiModel = "gemini-2.0-flash"
    };

    private static string GeminiResponse(string text) =>
        JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } } } } });

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
    public async Task ReviewAsync_SendsStructuredRequestAndFormatsValidatedReview()
    {
        var modelJson = """{"summary":"Adds a line.","findings":[{"file":"a.cs","line":1,"severity":"minor","message":"Unclear name."}]}""";
        var handler = new FakeHttpMessageHandler(GeminiResponse(modelJson));
        var service = new GeminiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync(Diff);

        Assert.Contains("`a.cs:1`: Unclear name.", result);

        var uri = handler.LastRequestUri!.ToString();
        Assert.Contains("generativelanguage.googleapis.com", uri);
        Assert.Contains("gemini-2.0-flash", uri);
        Assert.DoesNotContain("gemini-key", uri);
        Assert.Equal("gemini-key", handler.LastRequestHeaders!.GetValues("x-goog-api-key").Single());

        using var body = JsonDocument.Parse(handler.LastRequestContent!);
        var root = body.RootElement;
        Assert.Equal(ReviewPrompts.SystemPrompt, root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());

        var contents = root.GetProperty("contents");
        Assert.Equal(ReviewPrompts.FewShotExamples.Count * 2 + 1, contents.GetArrayLength());
        Assert.Equal("model", contents[1].GetProperty("role").GetString());
        var last = contents[contents.GetArrayLength() - 1];
        Assert.Equal("user", last.GetProperty("role").GetString());
        Assert.Contains("added line", last.GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.DoesNotContain("senior code reviewer", last.GetRawText());

        var config = root.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.Equal("OBJECT", config.GetProperty("responseSchema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReviewAsync_ReturnsFallback_WhenModelOutputIsNotValidJson()
    {
        var handler = new FakeHttpMessageHandler(GeminiResponse("Looks good overall."));
        var service = new GeminiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync(Diff);

        Assert.Equal(ReviewFormatter.InvalidOutputMessage, result);
    }
}
