using PRism.Api.Models;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class ReviewResultParserTests
{
    private const string Diff = "--- src/App.cs ---\n@@ -1,2 +1,3 @@\n+var x = 1;\n\n--- src/Util/Helpers.cs ---\n@@ -5 +5 @@\n+return null;\n";

    [Fact]
    public void Parse_AcceptsValidReview()
    {
        const string json = """
        {"summary":"Adds a helper.","findings":[{"file":"src/App.cs","line":1,"severity":"major","message":"x is unused."}]}
        """;

        var outcome = ReviewResultParser.Parse(json, Diff);

        Assert.True(outcome.Success);
        Assert.Empty(outcome.Problems);
        Assert.Equal("Adds a helper.", outcome.Result!.Summary);
        var finding = Assert.Single(outcome.Result.Findings);
        Assert.Equal(("src/App.cs", 1, "major", "x is unused."), (finding.File, finding.Line, finding.Severity, finding.Message));
    }

    [Fact]
    public void Parse_NormalisesSeverityLineAndFenceWrapping()
    {
        const string json = """
        ```json
        {"summary":"s","findings":[
          {"file":"src/App.cs","line":"3","severity":"CRITICAL","message":"a"},
          {"file":"/src/App.cs","line":0,"severity":"warning","message":"b"},
          {"file":"src/App.cs","severity":"Nit","message":"c"}
        ]}
        ```
        """;

        var findings = ReviewResultParser.Parse(json, Diff).Result!.Findings;

        Assert.Equal(["critical", "minor", "nit"], findings.Select(f => f.Severity));
        Assert.Equal([3, null, null], findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("src/App.cs", f.File));
    }

    [Fact]
    public void Parse_DropsInvalidFindingsButKeepsTheRest()
    {
        const string json = """
        {"summary":"s","findings":[
          {"file":"src/App.cs","line":1,"severity":"major","message":"kept"},
          {"file":"src/Missing.cs","line":1,"severity":"major","message":"file not in diff"},
          {"file":"src/App.cs","line":1,"severity":"catastrophic","message":"bad severity"},
          {"file":"src/App.cs","line":1,"severity":"minor","message":"  "},
          {"line":1,"severity":"minor","message":"no file"},
          "not an object"
        ]}
        """;

        var outcome = ReviewResultParser.Parse(json, Diff);

        Assert.True(outcome.Success);
        Assert.Equal("kept", Assert.Single(outcome.Result!.Findings).Message);
        Assert.Equal(5, outcome.Problems.Count);
        Assert.Contains(outcome.Problems, p => p.Contains("src/Missing.cs"));
    }

    [Fact]
    public void Parse_SortsBySeverityThenFileThenLine()
    {
        const string json = """
        {"summary":"","findings":[
          {"file":"src/Util/Helpers.cs","line":5,"severity":"nit","message":"1"},
          {"file":"src/Util/Helpers.cs","line":5,"severity":"critical","message":"2"},
          {"file":"src/App.cs","line":9,"severity":"major","message":"3"},
          {"file":"src/App.cs","line":2,"severity":"major","message":"4"}
        ]}
        """;

        var findings = ReviewResultParser.Parse(json, Diff).Result!.Findings;

        Assert.Equal(["2", "4", "3", "1"], findings.Select(f => f.Message));
    }

    [Fact]
    public void Parse_CapsNumberOfFindings()
    {
        var items = Enumerable.Range(1, 40).Select(i => $$"""{"file":"src/App.cs","line":{{i}},"severity":"nit","message":"m{{i}}"}""");
        var json = $$"""{"summary":"","findings":[{{string.Join(",", items)}}]}""";

        var outcome = ReviewResultParser.Parse(json, Diff);

        Assert.Equal(ReviewResultParser.MaxFindings, outcome.Result!.Findings.Count);
        Assert.Contains(outcome.Problems, p => p.Contains("beyond"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("This PR looks fine to me!")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"summary":"no findings key"}""")]
    [InlineData("""{"summary":"s","findings":"none"}""")]
    public void Parse_FailsForUnusableOutput(string? output)
    {
        var outcome = ReviewResultParser.Parse(output, Diff);

        Assert.False(outcome.Success);
        Assert.NotEmpty(outcome.Problems);
    }

    [Fact]
    public void ExtractFileNames_ReadsGitHubServiceHeaders()
    {
        Assert.Equal(["src/App.cs", "src/Util/Helpers.cs"], ReviewResultParser.ExtractFileNames(Diff).Order());
    }

    [Fact]
    public void FewShotExamples_AreValidAgainstTheirOwnDiffs()
    {
        foreach (var example in ReviewPrompts.FewShotExamples)
        {
            var outcome = ReviewResultParser.Parse(example.ExpectedJson, example.Diff);
            Assert.True(outcome.Success, string.Join("; ", outcome.Problems));
            Assert.Empty(outcome.Problems);
        }
    }
}
