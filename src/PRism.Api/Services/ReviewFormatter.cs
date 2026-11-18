using System.Text;
using PRism.Api.Models;

namespace PRism.Api.Services;

/// <summary>Renders a validated <see cref="ReviewResult"/> as the markdown comment posted on the PR.</summary>
public static class ReviewFormatter
{
    public const string Heading = "## 🔍 PRism review";

    public const string InvalidOutputMessage =
        "PRism: The AI provider returned a review that could not be validated, so nothing was posted. Check the PRism logs for details.";

    private static readonly Dictionary<string, string> SeverityLabels = new()
    {
        [ReviewSeverity.Critical] = "🔴 Critical",
        [ReviewSeverity.Major] = "🟠 Major",
        [ReviewSeverity.Minor] = "🟡 Minor",
        [ReviewSeverity.Nit] = "⚪ Nit",
    };

    public static string ToMarkdown(ReviewResult review)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Heading);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(review.Summary))
        {
            sb.AppendLine(review.Summary.Trim());
            sb.AppendLine();
        }

        if (review.Findings.Count == 0)
        {
            sb.AppendLine("No issues found. ✅");
            return sb.ToString().TrimEnd();
        }

        var counts = ReviewSeverity.All
            .Select(s => (Severity: s, Count: review.Findings.Count(f => f.Severity == s)))
            .Where(x => x.Count > 0)
            .Select(x => $"{x.Count} {x.Severity}");
        sb.AppendLine($"**{review.Findings.Count} finding(s):** {string.Join(", ", counts)}");
        sb.AppendLine();

        foreach (var finding in review.Findings)
        {
            var location = finding.Line is { } line ? $"{finding.File}:{line}" : finding.File;
            sb.AppendLine($"- **{SeverityLabels[finding.Severity]}** `{location}`: {finding.Message}");
        }

        return sb.ToString().TrimEnd();
    }
}
