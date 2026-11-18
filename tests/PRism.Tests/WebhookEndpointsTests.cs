using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class WebhookEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Secret = "webhook-test-secret";
    private const string OpenedPayload =
        """{"action":"opened","pull_request":{"number":42},"repository":{"name":"repo","owner":{"login":"octo"}}}""";

    private readonly IGitHubService _gitHub = Substitute.For<IGitHubService>();
    private readonly IAiReviewService _ai = Substitute.For<IAiReviewService>();
    private readonly HttpClient _client;

    public WebhookEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Bot:GitHubToken", "test-token");
            builder.UseSetting("Bot:WebhookSecret", Secret);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGitHubService>();
                services.RemoveAll<IAiReviewService>();
                services.AddSingleton(_gitHub);
                services.AddSingleton(_ai);
            });
        }).CreateClient();
    }

    private static HttpRequestMessage Webhook(string body, string eventType = "pull_request", string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook/github")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", eventType);
        request.Headers.Add("X-Hub-Signature-256", signature ?? Sign(body));
        return request;
    }

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public async Task OpenedPullRequest_FetchesDiffReviewsAndPostsComment()
    {
        _gitHub.GetDiffAsync("octo", "repo", 42).Returns("--- a.cs ---\n+x");
        _ai.ReviewAsync("--- a.cs ---\n+x").Returns("## review");

        var response = await _client.SendAsync(Webhook(OpenedPayload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _gitHub.Received(1).PostReviewCommentAsync("octo", "repo", 42, "## review");
    }

    [Fact]
    public async Task SynchronizeAction_IsAlsoReviewed()
    {
        _gitHub.GetDiffAsync(default!, default!, default).ReturnsForAnyArgs("diff");
        _ai.ReviewAsync(default!).ReturnsForAnyArgs("review");

        var response = await _client.SendAsync(Webhook(OpenedPayload.Replace("opened", "synchronize")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _gitHub.Received(1).PostReviewCommentAsync("octo", "repo", 42, "review");
    }

    [Theory]
    [InlineData("sha256=0000")]
    [InlineData("")]
    [InlineData("not-a-signature")]
    public async Task InvalidSignature_ReturnsUnauthorized_AndDoesNothing(string signature)
    {
        var response = await _client.SendAsync(Webhook(OpenedPayload, signature: signature));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_gitHub.ReceivedCalls());
        Assert.Empty(_ai.ReceivedCalls());
    }

    [Fact]
    public async Task NonPullRequestEvent_IsIgnored()
    {
        var response = await _client.SendAsync(Webhook("""{"zen":"hi"}""", eventType: "ping"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Event ignored", await response.Content.ReadAsStringAsync());
        Assert.Empty(_gitHub.ReceivedCalls());
    }

    [Fact]
    public async Task UnhandledAction_IsIgnored()
    {
        var response = await _client.SendAsync(Webhook(OpenedPayload.Replace("opened", "closed")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Action ignored", await response.Content.ReadAsStringAsync());
        Assert.Empty(_gitHub.ReceivedCalls());
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        var response = await _client.SendAsync(Webhook("{not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReviewFailure_ReturnsProblem_AndPostsNothing()
    {
        _gitHub.GetDiffAsync(default!, default!, default).ReturnsForAnyArgs("diff");
        _ai.ReviewAsync(default!).ThrowsAsyncForAnyArgs(new HttpRequestException("provider down"));

        var response = await _client.SendAsync(Webhook(OpenedPayload));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await _gitHub.DidNotReceiveWithAnyArgs().PostReviewCommentAsync(default!, default!, default, default!);
    }
}
