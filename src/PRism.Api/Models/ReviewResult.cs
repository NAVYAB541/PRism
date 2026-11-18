using System.Text.Json.Serialization;

namespace PRism.Api.Models;

/// <summary>The structured review the model is asked to return.</summary>
public class ReviewResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<ReviewFinding> Findings { get; set; } = new();
}

public class ReviewFinding
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    /// <summary>Line number in the new version of the file, or null when the finding is not tied to one line.</summary>
    [JsonPropertyName("line")]
    public int? Line { get; set; }

    /// <summary>One of <see cref="ReviewSeverity.All"/>.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public static class ReviewSeverity
{
    public const string Critical = "critical";
    public const string Major = "major";
    public const string Minor = "minor";
    public const string Nit = "nit";

    /// <summary>Allowed values, most severe first.</summary>
    public static readonly string[] All = [Critical, Major, Minor, Nit];
}
