using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PRism.Api.Services;

namespace PRism.Api.Models;

public class AzureOpenAiChatRequest
{
    [JsonPropertyName("messages")]
    public List<AzureOpenAiChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureOpenAiResponseFormat? ResponseFormat { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    public static AzureOpenAiChatRequest ForPrompt(string systemPrompt, string userPrompt) => new()
    {
        Messages = new List<AzureOpenAiChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        }
    };

    /// <summary>System prompt, few-shot turns, the diff, and a strict JSON schema for the reply.</summary>
    public static AzureOpenAiChatRequest ForReview(string diff)
    {
        var messages = new List<AzureOpenAiChatMessage> { new() { Role = "system", Content = ReviewPrompts.SystemPrompt } };
        foreach (var example in ReviewPrompts.FewShotExamples)
        {
            messages.Add(new() { Role = "user", Content = ReviewPrompts.BuildUserPrompt(example.Diff) });
            messages.Add(new() { Role = "assistant", Content = example.ExpectedJson.Trim() });
        }
        messages.Add(new() { Role = "user", Content = ReviewPrompts.BuildUserPrompt(diff) });

        return new AzureOpenAiChatRequest
        {
            Messages = messages,
            Temperature = 0.2,
            ResponseFormat = new AzureOpenAiResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new AzureOpenAiJsonSchema
                {
                    Name = "pull_request_review",
                    Strict = true,
                    Schema = ReviewPrompts.AzureResponseSchema()
                }
            }
        };
    }
}

public class AzureOpenAiResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_schema";

    [JsonPropertyName("json_schema")]
    public AzureOpenAiJsonSchema? JsonSchema { get; set; }
}

public class AzureOpenAiJsonSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("strict")]
    public bool Strict { get; set; }

    [JsonPropertyName("schema")]
    public JsonNode? Schema { get; set; }
}

public class AzureOpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class AzureOpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<AzureOpenAiChatChoice>? Choices { get; set; }
}

public class AzureOpenAiChatChoice
{
    [JsonPropertyName("message")]
    public AzureOpenAiChatMessage? Message { get; set; }
}
