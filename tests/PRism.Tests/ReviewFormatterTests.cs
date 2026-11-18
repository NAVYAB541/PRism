using PRism.Api.Models;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class ReviewFormatterTests
{
    [Fact]
    public void ToMarkdown_ListsFindingsWithSeverityAndLocation()
    {
        var review = new ReviewResult
        {
            Summary = "Adds login.",
            Findings =
            [
                new() { File = "src/A.cs", Line = 12, Severity = ReviewSeverity.Critical, Message = "SQL injection." },
                new() { File = "src/A.cs", Line = null, Severity = ReviewSeverity.Nit, Message = "Rename file." }
            ]
        };

        var markdown = ReviewFormatter.ToMarkdown(review);

        Assert.StartsWith(ReviewFormatter.Heading, markdown);
        Assert.Contains("Adds login.", markdown);
        Assert.Contains("**2 finding(s):** 1 critical, 1 nit", markdown);
        Assert.Contains("- **🔴 Critical** `src/A.cs:12`: SQL injection.", markdown);
        Assert.Contains("- **⚪ Nit** `src/A.cs`: Rename file.", markdown);
    }

    [Fact]
    public void ToMarkdown_SaysNoIssuesWhenFindingsAreEmpty()
    {
        var markdown = ReviewFormatter.ToMarkdown(new ReviewResult { Summary = "Docs only." });

        Assert.Contains("Docs only.", markdown);
        Assert.Contains("No issues found.", markdown);
        Assert.DoesNotContain("finding(s)", markdown);
    }
}
