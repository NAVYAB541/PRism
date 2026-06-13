namespace PRism.Api.Services;

public static class ReviewPrompts
{
    public const string SystemPrompt =
        "You are a senior code reviewer. Review the following pull request diff for " +
        "bugs, security issues, and style problems. Be concise, use markdown, and " +
        "reference filenames where relevant.";

    public const string NothingToReviewMessage =
        "PRism: No reviewable file changes were found in this pull request.";

    public static string BuildUserPrompt(string diff) =>
        $"Review this diff:\n\n{diff}";
}
