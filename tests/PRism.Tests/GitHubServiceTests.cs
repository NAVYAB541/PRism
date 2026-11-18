using NSubstitute;
using Octokit;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class GitHubServiceTests
{
    private readonly IGitHubClient _client = Substitute.For<IGitHubClient>();

    private static PullRequestFile File(string name, string? patch) =>
        new("abc", name, "modified", 1, 0, 1, null, null, null, patch, null);

    private void SetFiles(params PullRequestFile[] files) =>
        _client.PullRequest.Files("octo", "repo", 7).Returns(Task.FromResult<IReadOnlyList<PullRequestFile>>(files));

    [Fact]
    public async Task GetDiffAsync_WritesHeaderAndPatchPerFile_AndSkipsFilesWithoutPatch()
    {
        SetFiles(
            File("src/A.cs", "@@ -1 +1 @@\n+a"),
            File("assets/logo.png", null),
            File("src/B.cs", "@@ -2 +2 @@\n+b"));
        var service = new GitHubService(_client);

        var diff = await service.GetDiffAsync("octo", "repo", 7);

        Assert.Contains("--- src/A.cs ---\n@@ -1 +1 @@\n+a", diff.ReplaceLineEndings("\n"));
        Assert.Contains("--- src/B.cs ---", diff);
        Assert.DoesNotContain("logo.png", diff);
        Assert.Equal(["src/A.cs", "src/B.cs"], ReviewResultParser.ExtractFileNames(diff).Order());
    }

    [Fact]
    public async Task GetDiffAsync_ReturnsEmpty_WhenNoFileHasAPatch()
    {
        SetFiles(File("assets/logo.png", null));
        var service = new GitHubService(_client);

        Assert.Equal(string.Empty, await service.GetDiffAsync("octo", "repo", 7));
    }

    [Fact]
    public async Task GetDiffAsync_TruncatesVeryLargeDiffs()
    {
        SetFiles(File("big.txt", new string('x', 50_000)));
        var service = new GitHubService(_client);

        var diff = await service.GetDiffAsync("octo", "repo", 7);

        Assert.EndsWith("... (diff truncated)", diff);
        Assert.True(diff.Length < 12_100);
    }

    [Fact]
    public async Task PostReviewCommentAsync_CreatesIssueComment()
    {
        var service = new GitHubService(_client);

        await service.PostReviewCommentAsync("octo", "repo", 7, "Nice PR");

        await _client.Issue.Comment.Received(1).Create("octo", "repo", 7, "Nice PR");
    }
}
