using System.Text.Json;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class AzureOpenAiReviewServiceTests
{
    private const string Diff = "--- a.cs ---\n@@ -1 +1,2 @@\n+ added line";

    private static BotOptions CreateOptions() => new()
    {
        GitHubToken = "gh-token",
        WebhookSecret = "secret",
        AzureOpenAiEndpoint = "https://example.openai.azure.com",
        AzureOpenAiApiKey = "azure-key",
        AzureOpenAiDeployment = "gpt-4o",
        AzureOpenAiApiVersion = "2024-08-01-preview"
    };

    private static string ChatResponse(string content) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content } } } });

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
    public async Task ReviewAsync_SendsStructuredRequestAndFormatsValidatedReview()
    {
        var modelJson = """{"summary":"Adds a line.","findings":[{"file":"a.cs","line":null,"severity":"critical","message":"Leaks a secret."}]}""";
        var handler = new FakeHttpMessageHandler(ChatResponse(modelJson));
        var service = new AzureOpenAiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync(Diff);

        Assert.Contains("Adds a line.", result);
        Assert.Contains("`a.cs`: Leaks a secret.", result);
        Assert.Contains("example.openai.azure.com/openai/deployments/gpt-4o/chat/completions", handler.LastRequestUri!.ToString());
        Assert.Contains("api-version=2024-08-01-preview", handler.LastRequestUri!.ToString());

        using var body = JsonDocument.Parse(handler.LastRequestContent!);
        var root = body.RootElement;
        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(ReviewPrompts.FewShotExamples.Count * 2 + 2, messages.GetArrayLength());
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Contains("added line", messages[messages.GetArrayLength() - 1].GetProperty("content").GetString());

        var format = root.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task ReviewAsync_ReturnsFallback_WhenOutputFailsValidation()
    {
        var handler = new FakeHttpMessageHandler(ChatResponse("""{"summary":"s"}"""));
        var service = new AzureOpenAiReviewService(new HttpClient(handler), Options.Create(CreateOptions()));

        var result = await service.ReviewAsync(Diff);

        Assert.Equal(ReviewFormatter.InvalidOutputMessage, result);
    }
}
