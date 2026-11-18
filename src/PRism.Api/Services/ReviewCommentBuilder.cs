using Microsoft.Extensions.Logging;

namespace PRism.Api.Services;

/// <summary>Turns raw model output into the comment to post: validated markdown, or a safe fallback.</summary>
public static class ReviewCommentBuilder
{
    public static string Build(string? modelOutput, string diff, ILogger logger)
    {
        var outcome = ReviewResultParser.Parse(modelOutput, diff);

        if (!outcome.Success)
        {
            logger.LogWarning(
                "AI review failed validation: {Problems}. Raw output (truncated): {Output}",
                string.Join("; ", outcome.Problems),
                Truncate(modelOutput, 500));
            return ReviewFormatter.InvalidOutputMessage;
        }

        if (outcome.Problems.Count > 0)
        {
            logger.LogInformation("Dropped invalid review findings: {Problems}", string.Join("; ", outcome.Problems));
        }

        return ReviewFormatter.ToMarkdown(outcome.Result!);
    }

    private static string Truncate(string? text, int max) =>
        text is null ? "<null>" : text.Length <= max ? text : text[..max] + "…";
}
