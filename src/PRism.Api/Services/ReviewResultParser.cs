using System.Text.Json;
using System.Text.RegularExpressions;
using PRism.Api.Models;

namespace PRism.Api.Services;

/// <summary>
/// Parses and validates the model's JSON review. Invalid findings are dropped rather than
/// posted, so a single malformed item does not discard the whole review.
/// </summary>
public static partial class ReviewResultParser
{
    public const int MaxFindings = 25;
    private const int MaxMessageLength = 1_000;

    private static readonly Dictionary<string, string> SeverityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blocker"] = ReviewSeverity.Critical,
        ["high"] = ReviewSeverity.Major,
        ["error"] = ReviewSeverity.Major,
        ["medium"] = ReviewSeverity.Minor,
        ["warning"] = ReviewSeverity.Minor,
        ["low"] = ReviewSeverity.Nit,
        ["info"] = ReviewSeverity.Nit,
        ["style"] = ReviewSeverity.Nit,
    };

    public sealed record ParseOutcome(ReviewResult? Result, IReadOnlyList<string> Problems)
    {
        public bool Success => Result is not null;
    }

    /// <param name="modelOutput">Raw text returned by the model.</param>
    /// <param name="diff">The diff that was reviewed; findings for files not in it are dropped.</param>
    public static ParseOutcome Parse(string? modelOutput, string diff)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(modelOutput))
        {
            return new ParseOutcome(null, ["model returned no content"]);
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(StripCodeFences(modelOutput)).RootElement;
        }
        catch (JsonException ex)
        {
            return new ParseOutcome(null, [$"output is not valid JSON: {ex.Message}"]);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ParseOutcome(null, ["output is not a JSON object"]);
        }

        if (!root.TryGetProperty("findings", out var findingsElement) || findingsElement.ValueKind != JsonValueKind.Array)
        {
            return new ParseOutcome(null, ["\"findings\" is missing or not an array"]);
        }

        var summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()!.Trim()
            : string.Empty;

        var filesInDiff = ExtractFileNames(diff);
        var result = new ReviewResult { Summary = summary };
        var index = 0;

        foreach (var item in findingsElement.EnumerateArray())
        {
            index++;
            var finding = ValidateFinding(item, filesInDiff, out var problem);
            if (finding is null)
            {
                problems.Add($"finding {index}: {problem}");
                continue;
            }
            if (result.Findings.Count >= MaxFindings)
            {
                problems.Add($"dropped findings beyond the first {MaxFindings}");
                break;
            }
            result.Findings.Add(finding);
        }

        result.Findings = result.Findings
            .OrderBy(f => Array.IndexOf(ReviewSeverity.All, f.Severity))
            .ThenBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line ?? 0)
            .ToList();

        return new ParseOutcome(result, problems);
    }

    private static ReviewFinding? ValidateFinding(JsonElement item, HashSet<string> filesInDiff, out string? problem)
    {
        problem = null;
        if (item.ValueKind != JsonValueKind.Object)
        {
            problem = "not an object";
            return null;
        }

        var file = GetString(item, "file")?.Trim().TrimStart('/');
        if (string.IsNullOrEmpty(file))
        {
            problem = "missing file";
            return null;
        }
        if (filesInDiff.Count > 0 && !filesInDiff.Contains(file))
        {
            problem = $"file '{file}' is not part of the diff";
            return null;
        }

        var message = GetString(item, "message")?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            problem = "missing message";
            return null;
        }

        var severity = NormaliseSeverity(GetString(item, "severity"));
        if (severity is null)
        {
            problem = $"severity '{GetString(item, "severity")}' is not one of {string.Join("/", ReviewSeverity.All)}";
            return null;
        }

        return new ReviewFinding
        {
            File = file,
            Line = GetLine(item),
            Severity = severity,
            Message = message.Length > MaxMessageLength ? message[..MaxMessageLength] + "…" : message
        };
    }

    private static string? NormaliseSeverity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        if (ReviewSeverity.All.Contains(v)) return v;
        return SeverityAliases.GetValueOrDefault(v);
    }

    /// <summary>Accepts a positive integer or numeric string; anything else becomes null (a file-level finding).</summary>
    private static int? GetLine(JsonElement item)
    {
        if (!item.TryGetProperty("line", out var line)) return null;
        int value;
        if (line.ValueKind == JsonValueKind.Number && line.TryGetInt32(out value)) { }
        else if (line.ValueKind == JsonValueKind.String && int.TryParse(line.GetString(), out value)) { }
        else return null;
        return value > 0 ? value : null;
    }

    private static string? GetString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        var match = CodeFenceRegex().Match(trimmed);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    /// <summary>File names from the "--- path ---" headers that <see cref="GitHubService"/> writes.</summary>
    public static HashSet<string> ExtractFileNames(string diff) =>
        FileHeaderRegex().Matches(diff ?? string.Empty)
            .Select(m => m.Groups[1].Value.Trim())
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"^```(?:json)?\s*(.*?)\s*```$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"^--- (.+) ---\r?$", RegexOptions.Multiline)]
    private static partial Regex FileHeaderRegex();
}
