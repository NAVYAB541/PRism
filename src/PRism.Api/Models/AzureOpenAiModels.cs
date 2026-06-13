using System.Text.Json.Serialization;

namespace PRism.Api.Models;

public class AzureOpenAiChatRequest
{
    [JsonPropertyName("messages")]
    public List<AzureOpenAiChatMessage> Messages { get; set; } = new();

    public static AzureOpenAiChatRequest ForPrompt(string systemPrompt, string userPrompt) => new()
    {
        Messages = new List<AzureOpenAiChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        }
    };
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
