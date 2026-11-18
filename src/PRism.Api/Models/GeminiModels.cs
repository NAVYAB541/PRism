using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PRism.Api.Services;

namespace PRism.Api.Models;

public class GeminiRequest
{
    [JsonPropertyName("systemInstruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiGenerationConfig? GenerationConfig { get; set; }

    public static GeminiRequest ForPrompt(string prompt) => new()
    {
        Contents = new List<GeminiContent> { GeminiContent.FromText("user", prompt) }
    };

    /// <summary>System instruction, few-shot turns, the diff, and a JSON response schema.</summary>
    public static GeminiRequest ForReview(string diff)
    {
        var contents = new List<GeminiContent>();
        foreach (var example in ReviewPrompts.FewShotExamples)
        {
            contents.Add(GeminiContent.FromText("user", ReviewPrompts.BuildUserPrompt(example.Diff)));
            contents.Add(GeminiContent.FromText("model", example.ExpectedJson.Trim()));
        }
        contents.Add(GeminiContent.FromText("user", ReviewPrompts.BuildUserPrompt(diff)));

        return new GeminiRequest
        {
            SystemInstruction = new GeminiContent { Parts = [new() { Text = ReviewPrompts.SystemPrompt }] },
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = ReviewPrompts.GeminiResponseSchema(),
                Temperature = 0.2
            }
        };
    }
}

public class GeminiContent
{
    /// <summary>"user" or "model". Omitted for the system instruction.</summary>
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();

    public static GeminiContent FromText(string role, string text) => new()
    {
        Role = role,
        Parts = new List<GeminiPart> { new() { Text = text } }
    };
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; set; }

    [JsonPropertyName("responseSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? ResponseSchema { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}
