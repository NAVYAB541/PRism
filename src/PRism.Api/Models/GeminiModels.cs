using System.Text.Json.Serialization;

namespace PRism.Api.Models;

public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    public static GeminiRequest ForPrompt(string prompt) => new()
    {
        Contents = new List<GeminiContent>
        {
            new()
            {
                Parts = new List<GeminiPart> { new() { Text = prompt } }
            }
        }
    };
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
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
