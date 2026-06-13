using System.Text;
using Octokit;

namespace PRism.Api.Services;

public class GitHubService : IGitHubService
{
    /// <summary>Keeps the diff sent to the AI provider within a reasonable prompt size.</summary>
    private const int MaxDiffCharacters = 12_000;

    private readonly IGitHubClient _client;

    public GitHubService(IGitHubClient client)
    {
        _client = client;
    }

    public async Task<string> GetDiffAsync(string owner, string repo, int pullRequestNumber)
    {
        var files = await _client.PullRequest.Files(owner, repo, pullRequestNumber);

        var sb = new StringBuilder();
        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file.Patch))
            {
                continue;
            }

            sb.AppendLine($"--- {file.FileName} ---");
            sb.AppendLine(file.Patch);
            sb.AppendLine();
        }

        var diff = sb.ToString();
        return diff.Length > MaxDiffCharacters
            ? diff[..MaxDiffCharacters] + "\n... (diff truncated)"
            : diff;
    }

    public async Task PostReviewCommentAsync(string owner, string repo, int pullRequestNumber, string comment)
    {
        await _client.Issue.Comment.Create(owner, repo, pullRequestNumber, comment);
    }
}
