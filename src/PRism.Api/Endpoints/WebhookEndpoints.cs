using System.Text.Json;
using Microsoft.Extensions.Options;
using PRism.Api.Configuration;
using PRism.Api.Models;
using PRism.Api.Services;

namespace PRism.Api.Endpoints;

public static class WebhookEndpoints
{
    private static readonly string[] HandledActions = ["opened", "synchronize"];

    public static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhook/github", HandleGitHubWebhook);
    }

    private static async Task<IResult> HandleGitHubWebhook(
        HttpRequest request,
        IOptions<BotOptions> options,
        IGitHubService gitHubService,
        IAiReviewService aiReviewService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("PRism.Webhook");
        var botOptions = options.Value;

        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(rawBody);

        var signature = request.Headers["X-Hub-Signature-256"].ToString();
        if (!WebhookSignatureValidator.IsValid(signature, bodyBytes, botOptions.WebhookSecret))
        {
            return Results.Unauthorized();
        }

        var eventType = request.Headers["X-GitHub-Event"].ToString();
        if (eventType != "pull_request")
        {
            return Results.Ok(new { message = "Event ignored." });
        }

        PullRequestWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PullRequestWebhookPayload>(rawBody);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Invalid payload." });
        }

        if (payload?.PullRequest is null || payload.Repository is null || !HandledActions.Contains(payload.Action))
        {
            return Results.Ok(new { message = "Action ignored." });
        }

        var owner = payload.Repository.Owner.Login;
        var repo = payload.Repository.Name;
        var prNumber = payload.PullRequest.Number;

        try
        {
            var diff = await gitHubService.GetDiffAsync(owner, repo, prNumber);
            var review = await aiReviewService.ReviewAsync(diff);
            await gitHubService.PostReviewCommentAsync(owner, repo, prNumber, review);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review PR {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);
            return Results.Problem("Failed to generate or post review.");
        }

        return Results.Ok(new { message = "Review posted." });
    }
}
