namespace PRism.Api.Services;

public interface IGitHubService
{
    /// <summary>Fetches and concatenates the unified diff of all changed files in a pull request.</summary>
    Task<string> GetDiffAsync(string owner, string repo, int pullRequestNumber);

    /// <summary>Posts a summary comment on a pull request (issue) thread.</summary>
    Task PostReviewCommentAsync(string owner, string repo, int pullRequestNumber, string comment);
}
